namespace Pos.InventoryService.Domain.Models;

public abstract class InventoryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
}
