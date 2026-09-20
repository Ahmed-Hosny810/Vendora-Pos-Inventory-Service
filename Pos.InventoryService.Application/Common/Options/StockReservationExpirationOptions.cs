
namespace Pos.InventoryService.Application.Common.Options
{
    public class StockReservationExpirationOptions
    {
        public int CheckIntervalSeconds { get; set; } = 30;
        public int BatchSize { get; set; } = 100;
    }
}
