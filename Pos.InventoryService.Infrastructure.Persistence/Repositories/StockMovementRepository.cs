using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class StockMovementRepository : GenericRepositoryAsync<StockMovement, Guid>, IStockMovementRepository
    {
        private readonly ApplicationDbContext _context;

        public StockMovementRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<StockMovement> GetOpeningMovementByRequestIdAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
        {
            return await _context.StockMovements.FirstOrDefaultAsync(m => m.ReferenceId == requestId && m.TenantId == tenantId, cancellationToken);
        }
    }
}
