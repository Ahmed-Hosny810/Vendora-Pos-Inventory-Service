namespace Pos.InventoryService.Domain.Models;

/// <summary>An integration event saved in the same transaction as its inventory change.</summary>
public class OutboxMessage : InventoryEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

