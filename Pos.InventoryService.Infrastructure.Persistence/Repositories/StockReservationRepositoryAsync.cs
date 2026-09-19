
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class StockReservationRepositoryAsync : GenericRepositoryAsync<StockReservation, Guid>, IStockReservationRepositoryAsync
    {
        private readonly ApplicationDbContext _context;

        public StockReservationRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<StockReservation?> GetByIdAsync(
           Guid tenantId,
           Guid referenceId,
           CancellationToken cancellationToken)
        {
            return await _context.StockReservations
                .Include(x => x.Items)
                .SingleOrDefaultAsync(
                    x => x.TenantId == tenantId &&
                         x.ReferenceId == referenceId,
                    cancellationToken);
        }
    }
}
