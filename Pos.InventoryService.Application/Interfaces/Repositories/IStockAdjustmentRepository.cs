using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IStockAdjustmentRepository: IGenericRepositoryAsync<StockAdjustment, Guid>
    {
        Task<StockAdjustment?> GetDetailsAsync(
            Guid tenantId,
            Guid adjustmentId,
            CancellationToken cancellationToken);

        Task<StockAdjustment?> GetForUpdateAsync(
            Guid tenantId,
            Guid adjustmentId,
            CancellationToken cancellationToken);

        void RemoveItems(IEnumerable<StockAdjustmentItem> items);
    }
}
