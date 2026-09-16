
namespace Pos.InventoryService.Infrastructure.Persistence.ReadModels
{
    public class InventoryBranchReadModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class InventoryProductReadModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid UnitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool TrackInventory { get; set; }
    }

    public class InventoryVariantReadModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProductId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class InventoryUnitReadModel
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public bool IsDecimalAllowed { get; set; }
    }
}
