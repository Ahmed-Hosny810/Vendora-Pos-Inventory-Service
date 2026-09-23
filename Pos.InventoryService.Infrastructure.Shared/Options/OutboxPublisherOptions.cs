
namespace Pos.InventoryService.Infrastructure.Shared.Options
{
    public class OutboxPublisherOptions
    {
        public int PollingIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 20;
        public int PublishTimeoutSeconds { get; set; } = 10;
    }
}
