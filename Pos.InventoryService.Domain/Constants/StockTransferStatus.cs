namespace Pos.InventoryService.Domain.Constants;

public static class StockTransferStatus
{
    public const string Draft = "Draft";
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Dispatched = "Dispatched";
    public const string PartiallyReceived = "PartiallyReceived";
    public const string Received = "Received";
    public const string Cancelled = "Cancelled";
}
