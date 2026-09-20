using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Interfaces.Services
{
    public interface IStockReservationReleaseService
    {
        Task<Result> ReleaseAsync(
            Guid tenantId,
            Guid reservationId,
            CancellationToken cancellationToken);

        Task<Result> ExpireAsync(
            Guid tenantId,
            Guid reservationId,
            CancellationToken cancellationToken);
    }
}
