using Pos.InventoryService.Application.Features.StockReservations.DTOS;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IStockReservationRepositoryAsync:IGenericRepositoryAsync<StockReservation,Guid>
    {
        Task<StockReservation?> GetByReferenceIdAsync(
            Guid tenantId, Guid referenceId, CancellationToken cancellationToken);

        Task<StockReservation?> GetByIdAsync(
            Guid tenantId,
            Guid reservationId,
            CancellationToken cancellationToken);


        Task<IReadOnlyList<OverdueReservationDto>> GetOverdueActiveReservationsAsync(
             DateTime now,
             int batchSize,
             CancellationToken cancellationToken);


    }
}
