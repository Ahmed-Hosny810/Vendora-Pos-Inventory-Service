using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Domain.Models;
public class StockReservation : InventoryEntity
{
    public Guid BranchId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public string Status { get; set; } = StockReservationStatus.Active;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<StockReservationItem> Items { get; set; } = new List<StockReservationItem>();
}
