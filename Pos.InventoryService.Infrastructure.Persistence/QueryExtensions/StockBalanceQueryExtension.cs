using Pos.InventoryService.Application.Features.StockBalance.Queries.GetBalancesQuery;
using Pos.InventoryService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pos.InventoryService.Infrastructure.Persistence.QueryExtensions
{
    public static class StockBalanceQueryExtension
    {
        public static IQueryable<StockBalance> ApplyFilters(
            this IQueryable<StockBalance> query,
            Guid tenantId,
            StockBalanceFilter? filter)
        {
            query = query.Where(x => x.TenantId == tenantId);

            if (filter == null)
                return query;

            if (filter.BranchId!=Guid.Empty)
                query = query.Where(x => x.BranchId == filter.BranchId);

            if (filter.ProductId != Guid.Empty)
                query = query.Where(x => x.ProductId == filter.ProductId);

            if (filter.ProductVariantId.HasValue)
                query = query.Where(x => x.ProductVariantId == filter.ProductVariantId.Value);

            if (filter.QuantityOnHand.HasValue)
                query=query.Where(x=>x.QuantityOnHand == filter.QuantityOnHand);



            if (filter.QuantityReserved.HasValue)
                query = query.Where(x => x.QuantityReserved == filter.QuantityReserved.Value);

            if (filter.LowStockThreshold.HasValue)
                query = query.Where(x => x.LowStockThreshold == filter.LowStockThreshold.Value);

            if (filter.LowStockOnly)
                query = query.Where(x => x.QuantityOnHand <= x.LowStockThreshold);

            return query;
        }

        public static IQueryable<StockBalance> ApplyOrdering(
            this IQueryable<StockBalance> query,
            StockBalanceOrderKey orderKey,
            bool orderDescending)
        {
            var orderedQuery = orderKey switch
            {

                StockBalanceOrderKey.CreatedAt => orderDescending
                    ? query.OrderByDescending(x => x.CreatedAt)
                    : query.OrderBy(x => x.CreatedAt),

                StockBalanceOrderKey.UpdatedAt => orderDescending
                    ? query.OrderByDescending(x => x.UpdatedAt)
                    : query.OrderBy(x => x.UpdatedAt),

                StockBalanceOrderKey.QuantityOnHand => orderDescending
                    ? query.OrderByDescending(x => x.QuantityOnHand)
                    : query.OrderBy(x => x.QuantityOnHand),

                StockBalanceOrderKey.LowStockThreshold => orderDescending
                    ? query.OrderByDescending(x => x.LowStockThreshold)
                    : query.OrderBy(x => x.LowStockThreshold),

                _ => query.OrderBy(x => x.CreatedAt)
            };

            return orderedQuery.ThenBy(x => x.Id);
        }
    }
}
