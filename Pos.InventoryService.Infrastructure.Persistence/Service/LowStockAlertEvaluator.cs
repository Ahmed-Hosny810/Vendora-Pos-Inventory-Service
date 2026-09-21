using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Events;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.Infrastructure.Persistence.Service;

/// <summary>Stages alert transitions and outbox events before the unit of work commits.</summary>
public class LowStockAlertEvaluator
{
    private readonly ApplicationDbContext _context;

    public LowStockAlertEvaluator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EvaluateChangedBalancesAsync(CancellationToken cancellationToken)
    {
        _context.ChangeTracker.DetectChanges();
        var balances = _context.ChangeTracker.Entries<StockBalance>()
            .Where(entry => entry.State == EntityState.Added ||
                (entry.State == EntityState.Modified &&
                 (entry.Property(x => x.QuantityOnHand).IsModified ||
                  entry.Property(x => x.LowStockThreshold).IsModified)))
            .Select(entry => entry.Entity)
            .ToList();

        var now = DateTime.UtcNow;
        foreach (var balance in balances)
        {
            var alert = await _context.LowStockAlerts.SingleOrDefaultAsync(
                x => x.TenantId == balance.TenantId &&
                     x.BranchId == balance.BranchId &&
                     x.ProductId == balance.ProductId &&
                     x.ProductVariantId == balance.ProductVariantId &&
                     x.Status == LowStockAlertStatus.Active,
                cancellationToken);

            if (balance.QuantityOnHand > balance.LowStockThreshold)
            {
                if (alert != null)
                {
                    alert.Status = LowStockAlertStatus.Resolved;
                    alert.QuantityOnHand = balance.QuantityOnHand;
                    alert.Threshold = balance.LowStockThreshold;
                    alert.ResolvedAt = now;
                    alert.UpdatedAt = now;
                }
                continue;
            }

            if (alert != null)
            {
                // Still low: refresh the alert without producing another notification.
                alert.QuantityOnHand = balance.QuantityOnHand;
                alert.Threshold = balance.LowStockThreshold;
                alert.UpdatedAt = now;
                continue;
            }

            // A new low-stock episode gets a new alert and a stable event ID.
            alert = new LowStockAlert
            {
                Id = Guid.NewGuid(),
                TenantId = balance.TenantId,
                BranchId = balance.BranchId,
                ProductId = balance.ProductId,
                ProductVariantId = balance.ProductVariantId,
                QuantityOnHand = balance.QuantityOnHand,
                Threshold = balance.LowStockThreshold,
                Status = LowStockAlertStatus.Active,
                DetectedAt = now
            };
            _context.LowStockAlerts.Add(alert);

            var eventId = Guid.NewGuid();
            var notification = new LowStockDetected(
                eventId, balance.TenantId, alert.Id, balance.BranchId,
                balance.ProductId, balance.ProductVariantId,
                balance.QuantityOnHand, balance.LowStockThreshold, now);
            _context.OutboxMessages.Add(new OutboxMessage
            {
                Id = eventId,
                TenantId = balance.TenantId,
                EventType = nameof(LowStockDetected),
                Payload = JsonSerializer.Serialize(notification),
                OccurredAt = now
            });
        }
    }
}

