using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.StockMovements.Queries.GetMovementsHistoryQuery;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.QueryExtensions;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class StockMovementRepository : GenericRepositoryAsync<StockMovement, Guid>, IStockMovementRepository
    {
        private readonly ApplicationDbContext _context;

        public StockMovementRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<PagedResponse<IEnumerable<StockMovement>>> GetMovementsHistoryPagedAsync(Guid tenantId, StockMovementFilter? filter, StockMovementOrderKey orderKey, bool orderDescending, int pageNumber, int pageSize, CancellationToken cancellationToken)
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var query = _context.StockMovements.AsNoTracking();

            var filteredQuery=query.ApplyFilter(tenantId, filter);

            var totalCount=await filteredQuery.CountAsync(cancellationToken);

            var movements=await filteredQuery
                .ApplyOrdering(orderKey, orderDescending)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResponse<IEnumerable<StockMovement>>(movements,pageNumber,pageSize,totalCount);
        }

        public Task<bool> TransferReceiptExistsAsync(
            Guid tenantId,
            Guid transferId,
            Guid idempotencyKey,
            CancellationToken cancellationToken)
        {
            return _context.StockMovements.AnyAsync(
                x => x.TenantId == tenantId &&
                     x.ReferenceType == StockReferenceType.StockTransfer &&
                     x.ReferenceId == transferId &&
                     x.MovementType == StockMovementType.TransferIn &&
                     x.IdempotencyKey == idempotencyKey,
                cancellationToken);
        }

        public Task<bool> ReturnRestockExistsAsync(
            Guid tenantId, Guid returnId, CancellationToken cancellationToken)
        {
            return _context.StockMovements.AnyAsync(
                x => x.TenantId == tenantId &&
                     x.ReferenceType == StockReferenceType.Return &&
                     x.ReferenceId == returnId &&
                     x.MovementType == StockMovementType.Return,
                cancellationToken);
        }

        public async Task<StockMovement> GetOpeningMovementByRequestIdAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
        {
            return await _context.StockMovements.FirstOrDefaultAsync(m => m.ReferenceId == requestId && m.TenantId == tenantId, cancellationToken);
        }
    }
}
