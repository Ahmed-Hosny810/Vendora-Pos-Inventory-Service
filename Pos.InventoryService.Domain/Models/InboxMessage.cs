
namespace Pos.InventoryService.Domain.Models
{
    public class InboxMessage
    {
        public Guid EventId { get; set; }
        public string ConsumerName { get; set; } = string.Empty;
        public Guid TenantId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
