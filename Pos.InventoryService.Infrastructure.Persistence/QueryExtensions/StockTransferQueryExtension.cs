using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Persistence.QueryExtensions
{
    public static class StockTransferQueryExtension
    {
        public static IQueryable<StockTransfer> ApplyFilters(
            this IQueryable<StockTransfer> query,
            Guid tenantId,
            StockTransferFilter? filter)
        {
            query = query.Where(x => x.TenantId == tenantId);

            if (filter == null)
                return query;

            if (filter.FromBranchId.HasValue)
                query = query.Where(x => x.FromBranchId == filter.FromBranchId.Value);

            if (filter.ToBranchId.HasValue)
                query = query.Where(x => x.ToBranchId == filter.ToBranchId.Value);

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status.Trim();
                query = query.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(filter.TransferNumber))
            {
                var number = filter.TransferNumber.Trim();
                query = query.Where(x => x.TransferNumber == number);
            }

            if (filter.FromUtc.HasValue)
                query = query.Where(x => x.CreatedAt >= filter.FromUtc.Value);

            if (filter.ToUtcExclusive.HasValue)
                query = query.Where(x => x.CreatedAt < filter.ToUtcExclusive.Value);

            return query;
        }

        public static IOrderedQueryable<StockTransfer> ApplyOrdering(
            this IQueryable<StockTransfer> query,
            StockTransferOrderKey orderKey,
            bool orderDescending)
        {
            var orderedQuery = orderKey switch
            {
                StockTransferOrderKey.UpdatedAt => orderDescending
                    ? query.OrderByDescending(x => x.UpdatedAt)
                    : query.OrderBy(x => x.UpdatedAt),

                StockTransferOrderKey.TransferNumber => orderDescending
                    ? query.OrderByDescending(x => x.TransferNumber)
                    : query.OrderBy(x => x.TransferNumber),

                StockTransferOrderKey.Status => orderDescending
                    ? query.OrderByDescending(x => x.Status)
                    : query.OrderBy(x => x.Status),

                StockTransferOrderKey.ReceivedAt => orderDescending
                    ? query.OrderByDescending(x => x.ReceivedAt)
                    : query.OrderBy(x => x.ReceivedAt),

                _ => orderDescending
                    ? query.OrderByDescending(x => x.CreatedAt)
                    : query.OrderBy(x => x.CreatedAt)
            };

            return orderedQuery.ThenBy(x => x.Id);
        }
    }
}
