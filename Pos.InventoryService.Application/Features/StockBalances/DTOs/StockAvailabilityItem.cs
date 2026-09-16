
namespace Pos.InventoryService.Application.Features.StockBalances.DTOs
{
    public class StockAvailabilityItem
    {
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
    }
}
