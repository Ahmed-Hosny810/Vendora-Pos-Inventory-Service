using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Domain.Models;
public class StockTransfer : InventoryEntity
{
    public string TransferNumber { get; set; } = string.Empty;
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public string Status { get; set; } = StockTransferStatus.Draft;
    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}
