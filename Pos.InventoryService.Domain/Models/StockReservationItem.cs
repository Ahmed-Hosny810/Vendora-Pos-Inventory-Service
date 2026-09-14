namespace Pos.InventoryService.Domain.Models;
public class StockReservationItem : InventoryEntity
{
    public Guid ReservationId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public StockReservation Reservation { get; set; } = null!;
}
