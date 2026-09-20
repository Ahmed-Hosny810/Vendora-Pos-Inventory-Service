
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.StockReservations.DTOS;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Constants;
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

        public async Task<IReadOnlyList<OverdueReservationDto>> GetOverdueActiveReservationsAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
        {
            return await _context.StockReservations
                .Where(x =>
                x.Status == StockReservationStatus.Active &&
                x.ExpiresAt <= now)
                .OrderBy(x => x.ExpiresAt)
                .ThenBy(x => x.Id)
                .Take(batchSize)
                .Select(x => new OverdueReservationDto
                {
                    ReservationId = x.Id,
                    TenantId = x.TenantId

                })
                .ToListAsync(cancellationToken);
        }
    }
}
