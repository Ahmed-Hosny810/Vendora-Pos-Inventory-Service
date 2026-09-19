
namespace Pos.InventoryService.Application.Features.StockReservations.DTOS
{
    public class StockReservationItemDto
    {
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
    }
}
