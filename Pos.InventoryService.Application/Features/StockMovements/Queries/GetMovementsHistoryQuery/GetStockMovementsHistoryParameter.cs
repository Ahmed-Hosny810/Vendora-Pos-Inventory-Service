using Pos.InventoryService.Application.Parameters;


namespace Pos.InventoryService.Application.Features.StockMovements.Queries.GetMovementsHistoryQuery
{
    public class GetStockMovementsHistoryParameter: RequestParameter<StockMovementOrderKey>
    {
        public StockMovementFilter? Filter { get; set; }
    }

    public class StockMovementFilter
    {
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string? MovementType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtcExclusive { get; set; }
    }

    public enum StockMovementOrderKey
    {
        CreatedAt,
        LowStockThreshold,
        QuantityDelta
    }
}
