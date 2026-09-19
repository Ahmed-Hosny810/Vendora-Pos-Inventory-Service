
namespace Pos.InventoryService.Application.Interfaces.Services
{
    public interface IInventoryItemValidationService
    {
        Task ValidateStockItemAsync(
            Guid tenantId,
            Guid branchId,
            Guid productId,
            Guid? productVariantId,
            decimal quantity,
            CancellationToken cancellationToken);
    }
}
