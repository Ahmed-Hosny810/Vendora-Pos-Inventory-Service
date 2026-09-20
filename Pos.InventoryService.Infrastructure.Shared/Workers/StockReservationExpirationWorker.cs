
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Common.Options;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;

namespace Pos.InventoryService.Infrastructure.Shared.Workers
{
    public class StockReservationExpirationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<StockReservationExpirationWorker> _logger;
        private readonly StockReservationExpirationOptions _options;

        public StockReservationExpirationWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<StockReservationExpirationWorker> logger,
            IOptions<StockReservationExpirationOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(_options.CheckIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExpireReservationsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    // A failed scan must not permanently stop the worker.
                    _logger.LogError(
                        exception,
                        "Failed to scan for overdue stock reservations.");
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

        private async Task ExpireReservationsAsync(CancellationToken cancellationToken)

        {
            // 3. Create a scope for the overdue-reservation query.
            await using var queryScope = _scopeFactory.CreateAsyncScope();

            var repository = queryScope.ServiceProvider
                .GetRequiredService<IStockReservationRepositoryAsync>();

            var reservations =
                await repository.GetOverdueActiveReservationsAsync(
                    DateTime.UtcNow,
                    _options.BatchSize,
                    cancellationToken);

            foreach (var reservation in reservations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // 4. Use a fresh scope and context for this reservation.
                    await using var reservationScope =
                        _scopeFactory.CreateAsyncScope();

                    var releaseService = reservationScope.ServiceProvider
                        .GetRequiredService<IStockReservationReleaseService>();

                    // 5. The service rechecks status and expiry, then saves.
                    var result = await releaseService.ExpireAsync(
                        reservation.TenantId,
                        reservation.ReservationId,
                        cancellationToken);

                    // 6. Log business failures, including concurrency conflicts.
                    if (!result.IsSuccess)
                    {
                        _logger.LogWarning(
                            "Could not expire reservation {ReservationId} " +
                            "for tenant {TenantId}. Errors: {Errors}",
                            reservation.ReservationId,
                            reservation.TenantId,
                            string.Join("; ", result.Errors));
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Continue processing other reservations after a failure.
                    _logger.LogError(
                        exception,
                        "Failed to expire reservation {ReservationId} " +
                        "for tenant {TenantId}.",
                        reservation.ReservationId,
                        reservation.TenantId);
                }
            }
        }

    }
}