
namespace Pos.InventoryService.Application.Features.StockTransfers.DTOs
{
    public class StockTransferItemDto
    {
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
    }
}
