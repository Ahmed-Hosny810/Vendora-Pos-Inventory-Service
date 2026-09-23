using Pos.InventoryService.Application.Parameters;

namespace Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;

public class GetLowStockAlertsQueryParameter : RequestParameter<LowStockAlertOrderKey>
{
    public LowStockAlertFilter? Filter { get; set; }
    
}

public class LowStockAlertFilter
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string? Status { get; set; }
}

public enum LowStockAlertOrderKey
{
    DetectedAt,
    UpdatedAt,
    ResolvedAt,
    QuantityOnHand,
    Threshold,
    Status
}
