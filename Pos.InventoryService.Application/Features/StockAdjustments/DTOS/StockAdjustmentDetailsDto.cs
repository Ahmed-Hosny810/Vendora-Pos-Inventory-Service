
namespace Pos.InventoryService.Application.Features.StockAdjustments.DTOS
{
    public class StockAdjustmentDetailsDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }

        public string AdjustmentNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public Guid CreatedByUserId { get; set; }
        public Guid? ApprovedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PostedAt { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public List<StockAdjustmentItemDto> Items { get; set; } = new();
    }
}
