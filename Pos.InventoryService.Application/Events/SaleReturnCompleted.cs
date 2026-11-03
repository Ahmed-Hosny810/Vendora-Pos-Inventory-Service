
namespace Pos.InventoryService.Application.Events
{
    public record SaleReturnCompleted(
          Guid EventId,
          Guid TenantId,
          Guid ReturnId,
          Guid OriginalSaleId,
          Guid ReceivingBranchId,
          Guid ProcessedByUserId,
          List<SaleReturnCompletedItem> Items,
          DateTime OccurredAt);

    public record SaleReturnCompletedItem(
        Guid ReturnItemId,
        Guid ProductId,
        Guid? ProductVariantId,
        decimal Quantity,
        bool Restock,
        bool TrackInventory,
        string StockCondition);
}
