using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Domain.Models;
public class StockAdjustment : InventoryEntity
{
    public Guid BranchId { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Status { get; set; } = StockAdjustmentStatus.Draft;
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}
