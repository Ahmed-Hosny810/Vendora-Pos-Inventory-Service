
namespace Pos.InventoryService.Application.Features.StockMovements.DTOS
{
    public class StockMovementDto
    {
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string MovementType { get; set; } = null!;
        public decimal QuantityDelta { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }
        public string ReferenceType { get; set; } = null!;
        public Guid ReferenceId { get; set; }
        public decimal? LowStockThreshold { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
