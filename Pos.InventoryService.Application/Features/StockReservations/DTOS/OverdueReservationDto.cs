
namespace Pos.InventoryService.Application.Features.StockReservations.DTOS
{
    public class OverdueReservationDto
    {
        public Guid ReservationId { get; set; }
        public Guid TenantId { get; set; }
    }
}
