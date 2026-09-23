using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Infrastructure.Shared.Options;
using Pos.InventoryService.Infrastructure.Shared.Services;

namespace Pos.InventoryService.Infrastructure.Shared.Workers;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly OutboxPublisherOptions _options;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger,
        IOptions<OutboxPublisherOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var interval =
            TimeSpan.FromSeconds(_options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A database or other cycle-level failure must not end the worker.
                _logger.LogError(
                    exception,
                    "Failed to process the outbox batch.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task DispatchPendingMessagesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> pendingIds;

        // 1. Load pending IDs
        await using (var queryScope = _scopeFactory.CreateAsyncScope())
        {
            var repository = queryScope.ServiceProvider
                .GetRequiredService<IOutboxRepositoryAsync>();

            pendingIds = await repository.GetPendingIdsAsync(
                _options.BatchSize,
                cancellationToken);
        }

        // 2. Process each message using a fresh scope.
        foreach (var pendingId in pendingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var dispatchingScope =
                    _scopeFactory.CreateAsyncScope();

                var dispatcher = dispatchingScope.ServiceProvider
                    .GetRequiredService<OutboxDispatcher>();

                await dispatcher.DispatchAsync(
                    pendingId,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Application shutdown: stop processing.
                throw;
            }
            catch (Exception exception)
            {
                // This message remains pending; continue with the others.
                _logger.LogError(
                    exception,
                    "Failed to dispatch outbox message {MessageId}.",
                    pendingId);
            }
        }
    }
}