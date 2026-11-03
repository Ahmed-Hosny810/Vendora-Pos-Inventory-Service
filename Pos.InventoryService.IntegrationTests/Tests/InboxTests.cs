using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Infrastructure.Shared;
using Pos.InventoryService.Infrastructure.Shared.Options;
using Pos.InventoryService.Infrastructure.Shared.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.InventoryService.Application.Events;
using Pos.InventoryService.Application.Events.Constants;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Shared.Services;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class InboxTests : InventoryTest
{
    internal static SaleReturnCompleted Event(TestFixture fixture) => new(
        Guid.NewGuid(), fixture.TenantId, Guid.NewGuid(), Guid.NewGuid(), fixture.BranchId,
        Guid.Parse(fixture.CurrentUser.UserId!),
        [new(Guid.NewGuid(), fixture.ProductId, null, 2, true, true, StockCondition.Sellable)],
        DateTime.UtcNow);

    internal static async Task<Result> Dispatch(TestFixture fixture, SaleReturnCompleted message,
        Func<ApplicationDbContext, Task>? beforeSave = null, Guid? messageId = null, string? payload = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dispatcher = beforeSave == null
            ? services.GetRequiredService<IInboxEventDispatcher>()
            : new InboxEventDispatcher(services.GetServices<IIntegrationEventHandler>(),
                services.GetRequiredService<IInboxMessageRepositoryAsync>(),
                new BeforeSaveUnitOfWork(services.GetRequiredService<IUnitOfWork>(),
                    () => beforeSave(services.GetRequiredService<ApplicationDbContext>())),
                services.GetRequiredService<ILogger<InboxEventDispatcher>>());
        return await dispatcher.DispatchAsync(messageId ?? message.EventId,
            SalesEventTypes.SaleReturnCompleted, payload ?? JsonSerializer.Serialize(message), default);
    }

    [Fact]
    public void SharedRegistrations_ResolveDispatcherWorkerAndConfiguredOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMqConsumerOptions:SourceExchangeName"] = "sales.events"
        }).Build();
        using var fixture = new TestFixture(services => services.AddSharedInfrastructureServices(configuration));
        using var scope = fixture.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IInboxEventDispatcher>().Should().BeOfType<InboxEventDispatcher>();
        fixture.Services.GetServices<IHostedService>().Should().ContainSingle(x => x is RabbitMqConsumerWorker);
        fixture.Services.GetRequiredService<IOptions<RabbitMqConsumerOptions>>().Value
            .SourceExchangeName.Should().Be("sales.events");
        // Resolve only: no hosted services are started and no broker is contacted.
    }

    [Fact]
    public async Task ReturnEvent_RestocksOnce_AndRecordsMetadataAndActor()
    {
        await Fixture.SeedBalanceAsync(10, 3);
        var message = Event(Fixture);
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        var balance = await Fixture.BalanceAsync();
        balance.QuantityOnHand.Should().Be(12);
        balance.QuantityReserved.Should().Be(3);
        var movement = await Fixture.ReadAsync(db => db.StockMovements.SingleAsync());
        movement.MovementType.Should().Be(StockMovementType.Return);
        movement.ReferenceId.Should().Be(message.ReturnId);
        movement.IdempotencyKey.Should().Be(message.EventId);
        movement.CreatedByUserId.Should().Be(message.ProcessedByUserId);
        var inbox = await Fixture.ReadAsync(db => db.InboxMessages.SingleAsync());
        inbox.EventId.Should().Be(message.EventId);
        inbox.TenantId.Should().Be(message.TenantId);
        inbox.EventType.Should().Be(SalesEventTypes.SaleReturnCompleted);
        inbox.ConsumerName.Should().Be("inventory.restock-on-sale-return");
        inbox.OccurredAt.Should().Be(message.OccurredAt);
        inbox.ProcessedAt.Should().BeOnOrAfter(message.OccurredAt);
    }

    [Fact]
    public async Task IneligibleItems_AreSkipped_ButEventIsRecorded()
    {
        var message = Event(Fixture);
        message = message with { Items = [
            message.Items[0] with { Restock = false, StockCondition = StockCondition.Damaged },
            new(Guid.NewGuid(), Fixture.NonTrackedProductId, null, 1, true, false, StockCondition.Sellable)] };
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task MixedItems_OnlyEligibleItemsRestock()
    {
        var message = Event(Fixture);
        message.Items.Add(new(Guid.NewGuid(), Fixture.DecimalProductId, null, 1, false, true, StockCondition.Expired));
        message.Items.Add(new(Guid.NewGuid(), Fixture.NonTrackedProductId, null, 1, true, false, StockCondition.Sellable));
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(2);
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(1);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(1);
    }

    [Theory]
    [InlineData("mismatched-id")]
    [InlineData("malformed-json")]
    [InlineData("invalid-quantity")]
    [InlineData("damaged-restock")]
    [InlineData("missing-tenant")]
    [InlineData("missing-time")]
    public async Task InvalidEvent_DoesNotWriteAnything(string failure)
    {
        var message = Event(Fixture);
        if (failure == "invalid-quantity") message.Items[0] = message.Items[0] with { Quantity = 0 };
        if (failure == "damaged-restock") message.Items[0] = message.Items[0] with { StockCondition = StockCondition.Damaged };
        if (failure == "missing-tenant") message = message with { TenantId = Guid.Empty };
        if (failure == "missing-time") message = message with { OccurredAt = default };
        var result = await Dispatch(Fixture, message,
            messageId: failure == "mismatched-id" ? Guid.NewGuid() : null,
            payload: failure == "malformed-json" ? "{" : null);
        result.IsFailure.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task UnrelatedUniqueFailure_IsNotAcknowledged_AndFreshRetryWorks()
    {
        var message = Event(Fixture);
        Func<Task> dispatch = () => Dispatch(Fixture, message, _ =>
            throw new DuplicateStockWriteException(new Exception("Different unique constraint")));
        await dispatch.Should().ThrowAsync<DuplicateStockWriteException>();
        (await Fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(2);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task SaveFailure_RollsBackBalanceMovementAndInbox()
    {
        await Fixture.SeedBalanceAsync(10);
        var message = Event(Fixture);
        // Force a real SQL unique-index violation in the same SaveChanges transaction.
        Func<Task> dispatch = () => Dispatch(Fixture, message, db =>
        {
            db.StockBalances.Add(new StockBalance { Id = Guid.NewGuid(), TenantId = Fixture.TenantId,
                BranchId = Fixture.BranchId, ProductId = Fixture.ProductId, QuantityOnHand = 1 });
            return Task.CompletedTask;
        });
        await dispatch.Should().ThrowAsync<DuplicateStockWriteException>();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(10);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(0);
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(12);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentDuplicateDeliveries_CommitOneEffect_ThenRetryIsHarmless()
    {
        await Fixture.SeedBalanceAsync(10);
        var message = Event(Fixture);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task BeforeSave(ApplicationDbContext _)
        {
            if (Interlocked.Increment(ref arrived) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        async Task Attempt()
        {
            try { (await Dispatch(Fixture, message, BeforeSave)).IsSuccess.Should().BeTrue(); }
            catch (ConcurrencyConflictException) { /* Broker retries in a new scope. */ }
        }
        await Task.WhenAll(Attempt(), Attempt());
        (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(12);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(1);
        (await Fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(1);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConcurrentInboxInsert_RecognizesCommittedDuplicate()
    {
        var message = Event(Fixture);
        message.Items[0] = message.Items[0] with { Restock = false };
        // Another delivery commits after our inbox check but before our save.
        var result = await Dispatch(Fixture, message, async _ =>
            (await Dispatch(Fixture, message)).IsSuccess.Should().BeTrue());
        result.IsSuccess.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.InboxMessages.CountAsync())).Should().Be(1);
    }
}

internal sealed class BeforeSaveUnitOfWork(IUnitOfWork inner, Func<Task> beforeSave) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await beforeSave();
        return await inner.SaveChangesAsync(cancellationToken);
    }
}
