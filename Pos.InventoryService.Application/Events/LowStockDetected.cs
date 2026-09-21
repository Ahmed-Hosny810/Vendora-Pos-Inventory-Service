namespace Pos.InventoryService.Application.Events;

public record LowStockDetected(
    Guid EventId,
    Guid TenantId,
    Guid AlertId,
    Guid BranchId,
    Guid ProductId,
    Guid? ProductVariantId,
    decimal QuantityOnHand,
    decimal Threshold,
    DateTime DetectedAt);

