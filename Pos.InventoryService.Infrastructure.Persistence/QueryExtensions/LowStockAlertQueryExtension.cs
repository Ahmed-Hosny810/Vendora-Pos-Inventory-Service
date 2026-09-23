using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.QueryExtensions;

public static class LowStockAlertQueryExtension
{
    public static IQueryable<LowStockAlert> ApplyFilters(
        this IQueryable<LowStockAlert> query, Guid tenantId, LowStockAlertFilter? filter)
    {
        query = query.Where(x => x.TenantId == tenantId);
        if (filter == null)
            return query;

        if (filter.BranchId != Guid.Empty)
            query = query.Where(x => x.BranchId == filter.BranchId);
        if (filter.ProductId != Guid.Empty)
            query = query.Where(x => x.ProductId == filter.ProductId);
        if (filter.ProductVariantId.HasValue)
            query = query.Where(x => x.ProductVariantId == filter.ProductVariantId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        return query;
    }

    public static IQueryable<LowStockAlert> ApplyOrdering(
        this IQueryable<LowStockAlert> query, LowStockAlertOrderKey orderKey, bool orderDescending)
    {
        var orderedQuery = orderKey switch
        {
            LowStockAlertOrderKey.DetectedAt => orderDescending
                ? query.OrderByDescending(x => x.DetectedAt)
                : query.OrderBy(x => x.DetectedAt),
            LowStockAlertOrderKey.UpdatedAt => orderDescending
                ? query.OrderByDescending(x => x.UpdatedAt)
                : query.OrderBy(x => x.UpdatedAt),
            LowStockAlertOrderKey.ResolvedAt => orderDescending
                ? query.OrderByDescending(x => x.ResolvedAt)
                : query.OrderBy(x => x.ResolvedAt),
            LowStockAlertOrderKey.QuantityOnHand => orderDescending
                ? query.OrderByDescending(x => x.QuantityOnHand)
                : query.OrderBy(x => x.QuantityOnHand),
            LowStockAlertOrderKey.Threshold => orderDescending
                ? query.OrderByDescending(x => x.Threshold)
                : query.OrderBy(x => x.Threshold),
            LowStockAlertOrderKey.Status => orderDescending
                ? query.OrderByDescending(x => x.Status)
                : query.OrderBy(x => x.Status),
            _ => query.OrderByDescending(x => x.DetectedAt)
        };

        return orderedQuery.ThenBy(x => x.Id);
    }
}
