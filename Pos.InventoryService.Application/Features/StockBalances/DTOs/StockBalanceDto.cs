

namespace Pos.InventoryService.Application.Features.StockBalances.DTOs
{
    public class StockBalanceDto
    {
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal QuantityReserved { get; set; }
        public decimal LowStockThreshold { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal AvailableQuantity => QuantityOnHand - QuantityReserved;
    }
}
