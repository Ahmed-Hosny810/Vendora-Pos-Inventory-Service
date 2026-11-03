using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Events.Constants;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Infrastructure.Shared.Options;
using Pos.InventoryService.Infrastructure.Shared.Services;
using Pos.InventoryService.Infrastructure.Shared.Workers;
using RabbitMQ.Client;

namespace Pos.InventoryService.IntegrationTests.Tests;

[Trait("Category", "RabbitMq")]
public class RabbitMqConsumerTests
{
    [RabbitMqFact]
    public async Task MatchingRoutingKey_ProcessesDuplicatesOnce_AndAcknowledgesAfterCommit()
    {
        using var fixture = new TestFixture();
        await using var broker = await ConsumerHarness.StartAsync(fixture);
        var message = InboxTests.Event(fixture);
        var payload = JsonSerializer.Serialize(message);
        await broker.PublishAsync(message.EventId, payload);
        await broker.PublishAsync(message.EventId, payload);
        await ConsumerHarness.UntilAsync(async () =>
            await fixture.ReadAsync(db => db.InboxMessages.CountAsync()) == 1);
        // Stop closes the consumer connection; unacknowledged deliveries would reappear.
        await broker.WaitForDrainAsync();
        await broker.StopAsync();
        (await broker.Channel.BasicGetAsync(broker.Options.QueueName, true)).Should().BeNull();
        (await fixture.BalanceAsync()).QuantityOnHand.Should().Be(2);
        (await fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(1);
    }

    [RabbitMqFact]
    public async Task InvalidPayload_IsDeadLettered_WithoutStockOrInboxWrites()
    {
        using var fixture = new TestFixture();
        await using var broker = await ConsumerHarness.StartAsync(fixture);
        var eventId = Guid.NewGuid();
        await broker.PublishAsync(eventId, "{");
        BasicGetResult? dead = null;
        await ConsumerHarness.UntilAsync(async () =>
            (dead = await broker.Channel.BasicGetAsync(broker.Options.DeadLetterQueue, true)) != null);
        dead!.BasicProperties.MessageId.Should().Be(eventId.ToString());
        (await fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(0);
        (await fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(0);
    }

    [RabbitMqFact]
    public async Task TransientFailure_IsRedelivered_AndOnlySuccessfulAttemptCommits()
    {
        var attempts = new DeliveryAttempts();
        using var fixture = new TestFixture(services =>
        {
            services.AddScoped<InboxEventDispatcher>();
            services.AddScoped<IInboxEventDispatcher>(provider => new FailFirstDispatcher(
                provider.GetRequiredService<InboxEventDispatcher>(), attempts));
        });
        await using var broker = await ConsumerHarness.StartAsync(fixture);
        var message = InboxTests.Event(fixture);
        await broker.PublishAsync(message.EventId, JsonSerializer.Serialize(message));
        await ConsumerHarness.UntilAsync(async () =>
            await fixture.ReadAsync(db => db.InboxMessages.CountAsync()) == 1);
        attempts.Count.Should().Be(2);
        (await fixture.BalanceAsync()).QuantityOnHand.Should().Be(2);
        (await fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(1);
    }

    private sealed class DeliveryAttempts { public int Count; }
    private sealed class FailFirstDispatcher(InboxEventDispatcher inner, DeliveryAttempts attempts) : IInboxEventDispatcher
    {
        public Task<Result> DispatchAsync(Guid eventId, string eventType, string payload, CancellationToken ct)
        {
            if (Interlocked.Increment(ref attempts.Count) == 1)
                throw new IOException("Simulated transient processing failure");
            return inner.DispatchAsync(eventId, eventType, payload, ct);
        }
    }
}

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INVENTORY_TEST_RABBITMQ_URI")))
            Skip = "Requires INVENTORY_TEST_RABBITMQ_URI pointing to a test broker/vhost.";
    }
}

internal sealed class ConsumerHarness : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqConsumerWorker _worker;
    private bool _stopped;
    public IChannel Channel { get; }
    public RabbitMqConsumerOptions Options { get; }

    private ConsumerHarness(IConnection connection, IChannel channel,
        RabbitMqConsumerWorker worker, RabbitMqConsumerOptions options)
    { _connection = connection; Channel = channel; _worker = worker; Options = options; }

    public static async Task<ConsumerHarness> StartAsync(TestFixture fixture)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(Environment.GetEnvironmentVariable("INVENTORY_TEST_RABBITMQ_URI")!),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
        };
        var name = "inventory.integration." + Guid.NewGuid().ToString("N");
        var options = new RabbitMqConsumerOptions
        {
            SourceExchangeName = name + ".events", QueueName = name,
            DeadLetterExchange = name + ".dlx", DeadLetterQueue = name + ".dlq",
            PrefetchCount = 1, MaxDeliveryAttempts = 3
        };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
        var worker = new RabbitMqConsumerWorker(
            Microsoft.Extensions.Options.Options.Create(new RabbitMqOptions
            {
                HostName = factory.HostName, Port = factory.Port, UserName = factory.UserName,
                Password = factory.Password, VirtualHost = factory.VirtualHost
            }), Microsoft.Extensions.Options.Options.Create(options),
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RabbitMqConsumerWorker>.Instance);
        var harness = new ConsumerHarness(connection, channel, worker, options);
        try
        {
            await worker.StartAsync(default);
            // A passive check uses a separate channel: a missing queue closes that channel.
            await UntilAsync(async () =>
            {
                if (worker.ExecuteTask?.IsFaulted == true) await worker.ExecuteTask;
                await using var probe = await connection.CreateChannelAsync();
                try { return (await probe.QueueDeclarePassiveAsync(name)).ConsumerCount == 1; }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException) { return false; }
            });
            return harness;
        }
        catch { await harness.DisposeAsync(); throw; }
    }

    public async Task PublishAsync(Guid id, string payload)
    {
        var properties = new BasicProperties { MessageId = id.ToString(),
            Type = SalesEventTypes.SaleReturnCompleted, ContentType = "application/json", Persistent = true };
        await Channel.BasicPublishAsync(Options.SourceExchangeName, "sales.return.completed.v1",
            mandatory: true, basicProperties: properties, body: Encoding.UTF8.GetBytes(payload));
    }

    public async Task WaitForDrainAsync()
    {
        await UntilAsync(async () => (await Channel.QueueDeclarePassiveAsync(Options.QueueName)).MessageCount == 0);
        // Ensure the consumer has finished its final acknowledgement before shutting down.
        await Task.Delay(250);
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _worker.StopAsync(timeout.Token);
        _stopped = true;
    }

    public static async Task UntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
            if (Channel.IsOpen)
            {
                await Channel.QueueDeleteAsync(Options.QueueName, false, false);
                await Channel.QueueDeleteAsync(Options.DeadLetterQueue, false, false);
                await Channel.ExchangeDeleteAsync(Options.SourceExchangeName, false);
                await Channel.ExchangeDeleteAsync(Options.DeadLetterExchange, false);
            }
        }
        finally
        {
            _worker.Dispose();
            await Channel.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
