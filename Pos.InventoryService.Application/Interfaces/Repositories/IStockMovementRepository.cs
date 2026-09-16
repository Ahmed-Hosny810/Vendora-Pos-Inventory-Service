using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IStockMovementRepository:IGenericRepositoryAsync<StockMovement,Guid>
    {
        Task<StockMovement> GetOpeningMovementByRequestIdAsync(Guid tenantId,Guid requestId , CancellationToken cancellationToken);
    }
}
