using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetBalancesQuery;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.QueryExtensions;


namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class StockBalanceRepositoryAsync : GenericRepositoryAsync<StockBalance, Guid>, IStockBalanceRepositoryAsync
    {
        private readonly ApplicationDbContext _context;

        public StockBalanceRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

       
        public async Task<StockBalance?> GetProductStockBalanceAsync(
             Guid tenantId,
             Guid branchId,
             Guid productId,
             Guid? productVariantId,
             CancellationToken cancellationToken)
        {
            return await _context.StockBalances
                .SingleOrDefaultAsync(
                    sb => sb.TenantId == tenantId
                       && sb.BranchId == branchId
                       && sb.ProductId == productId
                       && sb.ProductVariantId == productVariantId,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<StockBalance>> GetBatchStockAvailabilityAsync(Guid tenantId,Guid branchId,List<StockAvailabilityItem> items,
            CancellationToken cancellationToken)
        {
            if (items.Count == 0)
                return Array.Empty<StockBalance>();

            var requestedPairs = items
                .Select(x => (x.ProductId, x.ProductVariantId))
                .ToHashSet();

            var variantIds = items
                .Where(x => x.ProductVariantId.HasValue)
                .Select(x => x.ProductVariantId!.Value)
                .Distinct()
                .ToList();

            var productIds = items
                .Where(x => !x.ProductVariantId.HasValue)
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var balances = await _context.StockBalances
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.BranchId == branchId &&
                    (
                        (x.ProductVariantId.HasValue &&
                         variantIds.Contains(x.ProductVariantId.Value))
                        ||
                        (!x.ProductVariantId.HasValue &&
                         productIds.Contains(x.ProductId))
                    ))
                .ToListAsync(cancellationToken);

            return balances
                .Where(x => requestedPairs.Contains(
                    (x.ProductId, x.ProductVariantId)))
                .ToList();
        }

        public async Task<PagedResponse<IEnumerable<StockBalance>>> GetStockBalancesPagedResponseAsync(Guid tenantId, StockBalanceFilter? filter, StockBalanceOrderKey orderKey, bool orderDescending, int pageNumber, int pageSize, CancellationToken cancellationToken)
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var query = _context.StockBalances.AsNoTracking();

            var filteredQuery = query.ApplyFilters(
                tenantId,
                filter);

            var totalCount = await filteredQuery
                .CountAsync(cancellationToken);

            var stockBalances = await filteredQuery
               .ApplyOrdering(orderKey, orderDescending)
               .Skip((pageNumber - 1) * pageSize)
               .Take(pageSize)
               .ToListAsync(cancellationToken);

            return new PagedResponse<IEnumerable<StockBalance>>(
                stockBalances,
                pageNumber,
                pageSize,
                totalCount);
        }
    }
}
