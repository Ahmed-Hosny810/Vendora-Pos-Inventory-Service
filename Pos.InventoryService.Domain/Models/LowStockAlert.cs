using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Domain.Models;
public class LowStockAlert : InventoryEntity
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal Threshold { get; set; }
    public string Status { get; set; } = LowStockAlertStatus.Active;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
