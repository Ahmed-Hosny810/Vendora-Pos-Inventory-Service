

namespace Pos.InventoryService.Application.Features.StockAdjustments.DTOS
{
    public class CreateStockAdjustmentItemDto
    {
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal NewQuantity { get; set; }
    }
}
