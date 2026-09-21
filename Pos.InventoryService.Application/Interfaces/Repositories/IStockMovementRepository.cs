using Pos.InventoryService.Application.Features.StockMovements.Queries.GetMovementsHistoryQuery;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IStockMovementRepository:IGenericRepositoryAsync<StockMovement,Guid>
    {
        Task<StockMovement> GetOpeningMovementByRequestIdAsync(Guid tenantId,Guid requestId , CancellationToken cancellationToken);
        Task<PagedResponse<IEnumerable<StockMovement>>> GetMovementsHistoryPagedAsync(
            Guid tenantId,
            StockMovementFilter? filter,
            StockMovementOrderKey orderKey,
            bool orderDescending,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken
            );


        Task<bool> TransferReceiptExistsAsync(
            Guid tenantId,
            Guid transferId,
            Guid idempotencyKey,
            CancellationToken cancellationToken);
    }
}
