namespace Pos.InventoryService.Domain.Models;
public class StockTransferItem : InventoryEntity
{
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public StockTransfer Transfer { get; set; } = null!;
}
