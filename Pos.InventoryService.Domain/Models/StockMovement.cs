
namespace Pos.InventoryService.Domain.Models;
public class StockMovement : InventoryEntity
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal QuantityDelta { get; set; }
    public decimal BeforeQty { get; set; }
    public decimal AfterQty { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public decimal? LowStockThreshold { get; set; }

    public Guid? IdempotencyKey { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
