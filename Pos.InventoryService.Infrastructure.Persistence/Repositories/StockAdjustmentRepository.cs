using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class StockAdjustmentRepository: GenericRepositoryAsync<StockAdjustment, Guid> , IStockAdjustmentRepository
    {
        private readonly ApplicationDbContext _context;

        public StockAdjustmentRepository(ApplicationDbContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task<StockAdjustment?> GetDetailsAsync(
            Guid tenantId,
            Guid adjustmentId,
            CancellationToken cancellationToken)
        {
            return await _context.StockAdjustments
                .AsNoTracking()
                .Include(x => x.Items)
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId &&
                         x.Id == adjustmentId,
                    cancellationToken);
        }

        public async Task<StockAdjustment?> GetForUpdateAsync(
             Guid tenantId,
             Guid adjustmentId,
             CancellationToken cancellationToken)
        {
            return await _context.StockAdjustments
                .Include(x => x.Items)
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId &&
                         x.Id == adjustmentId,
                    cancellationToken);
        }

        public void RemoveItems(IEnumerable<StockAdjustmentItem> items)
        {
            _context.StockAdjustmentItems.RemoveRange(items);
        }

    }
}
