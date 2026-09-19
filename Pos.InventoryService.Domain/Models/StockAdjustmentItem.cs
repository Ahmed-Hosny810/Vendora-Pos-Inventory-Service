namespace Pos.InventoryService.Domain.Models;
public class StockAdjustmentItem : InventoryEntity
{
    public Guid AdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal OldQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal QuantityDelta { get; set; }
    public byte[] BalanceRowVersionAtCount { get; set; } = Array.Empty<byte>();
    public StockAdjustment Adjustment { get; set; } = null!;
}
