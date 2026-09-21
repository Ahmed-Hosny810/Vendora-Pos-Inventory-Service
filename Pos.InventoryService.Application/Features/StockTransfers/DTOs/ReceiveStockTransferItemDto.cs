
namespace Pos.InventoryService.Application.Features.StockTransfers.DTOs
{
    public class ReceiveStockTransferItemDto
    {
        public Guid TransferItemId { get; set; }

        public decimal Quantity { get; set; }
    }
}
