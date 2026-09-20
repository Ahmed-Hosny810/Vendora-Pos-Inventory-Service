using Pos.InventoryService.Application.Parameters;

namespace Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery
{
    public class GetStockTransfersQueryParameter:RequestParameter<StockTransferOrderKey>
    {
        public StockTransferFilter? Filter { get; set; }
    }

    public class StockTransferFilter
    {
        public Guid? FromBranchId { get; set; }
        public Guid? ToBranchId { get; set; }
        public string? Status { get; set; }
        public string? TransferNumber { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
    }
    public enum StockTransferOrderKey
    {
        CreatedAt,
        UpdatedAt,
        TransferNumber,
        Status,
        ReceivedAt
    }
}
