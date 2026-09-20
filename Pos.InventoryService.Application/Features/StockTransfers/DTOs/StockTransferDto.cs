namespace Pos.InventoryService.Application.Features.StockTransfers.DTOS
{
    public class StockTransferDto
    {
        public Guid Id { get; set; }
        public string TransferNumber { get; set; } = string.Empty;
        public Guid FromBranchId { get; set; }
        public Guid ToBranchId { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid RequestedByUserId { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
    }
}

