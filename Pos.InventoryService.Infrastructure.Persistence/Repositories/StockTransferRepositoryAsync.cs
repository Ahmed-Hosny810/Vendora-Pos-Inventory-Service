using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.QueryExtensions;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class StockTransferRepositoryAsync
        : GenericRepositoryAsync<StockTransfer, Guid>, IStockTransferRepositoryAsync
    {
        private readonly ApplicationDbContext _context;

        public StockTransferRepositoryAsync(ApplicationDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<StockTransfer?> GetByIdAsync(
            Guid tenantId,
            Guid transferId,
            CancellationToken cancellationToken)
        {
            return await _context.StockTransfers
                .Include(x => x.Items)
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Id == transferId,
                    cancellationToken);
        }

        public async Task<PagedResponse<IEnumerable<StockTransfer>>> GetPagedAsync(
            Guid tenantId,
            StockTransferFilter? filter,
            StockTransferOrderKey orderKey,
            bool orderDescending,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 50);

            var query = _context.StockTransfers
                .AsNoTracking()
                .ApplyFilters(tenantId, filter);

            var totalCount = await query.CountAsync(cancellationToken);

            var transfers = await query
                .ApplyOrdering(orderKey, orderDescending)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResponse<IEnumerable<StockTransfer>>(
                transfers, pageNumber, pageSize, totalCount);
        }

        public void RemoveItems(IEnumerable<StockTransferItem> items)
        {
            _context.StockTransferItems.RemoveRange(items);
        }
    }
}
