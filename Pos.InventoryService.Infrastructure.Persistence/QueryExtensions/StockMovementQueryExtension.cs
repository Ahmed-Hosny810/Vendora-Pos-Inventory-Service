using Pos.InventoryService.Application.Features.StockMovements.Queries.GetMovementsHistoryQuery;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.QueryExtensions
{
    public static class StockMovementQueryExtension
    {
        public static IQueryable<StockMovement> ApplyFilter(this IQueryable<StockMovement> query,Guid tenantId,StockMovementFilter? filter)
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

            if(!string.IsNullOrWhiteSpace(filter.MovementType))
                query=query.Where(x=>x.MovementType == filter.MovementType);

            if (filter.FromUtc.HasValue)
                query = query.Where(x => x.CreatedAt >= filter.FromUtc.Value);

            if (filter.ToUtcExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < filter.ToUtcExclusive.Value);

            return query;
        }

        public static IQueryable<StockMovement> ApplyOrdering(this IQueryable<StockMovement> query,StockMovementOrderKey orderKey, bool orderDescending)
        {
            var orderedQuery = orderKey switch
            {
                StockMovementOrderKey.CreatedAt => orderDescending
                    ? query.OrderByDescending(x => x.CreatedAt)
                    : query.OrderBy(x => x.CreatedAt),

                StockMovementOrderKey.LowStockThreshold => orderDescending
                    ? query.OrderByDescending(x => x.LowStockThreshold)
                    : query.OrderBy(x => x.LowStockThreshold),

                StockMovementOrderKey.QuantityDelta => orderDescending
                    ? query.OrderByDescending(x => x.QuantityDelta)
                    : query.OrderBy(x => x.QuantityDelta),

                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            return orderedQuery.ThenBy(x => x.Id);
        }
    }
}
