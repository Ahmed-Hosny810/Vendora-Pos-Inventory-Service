
namespace Pos.InventoryService.Application.Features.StockAdjustments.DTOS
{
    public class StockAdjustmentItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }

        public decimal OldQuantity { get; set; }
        public decimal NewQuantity { get; set; }
        public decimal QuantityDelta { get; set; }
    }
}
