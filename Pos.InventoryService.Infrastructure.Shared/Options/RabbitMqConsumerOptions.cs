
namespace Pos.InventoryService.Infrastructure.Shared.Options
{
    public class RabbitMqConsumerOptions
    {
        public string SourceExchangeName { get; set; } = string.Empty;
        public string QueueName { get; set; } = "inventory.sales-events";
        public string DeadLetterExchange { get; set; } = "inventory.sales-events.dlx";
        public string DeadLetterQueue { get; set; } = "inventory.sales-events.dlq";
        public string[] RoutingKeys { get; set; } = { "sales.return.completed.v1" };
        public ushort PrefetchCount { get; set; } = 10;
        public int MaxDeliveryAttempts { get; set; } = 5;
    }
}
