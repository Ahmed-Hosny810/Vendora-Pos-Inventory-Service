
namespace Pos.InventoryService.Application.Common.Constants
{
    public static class InventoryPolicies
    {
        public const string TenantRequired = "TenantRequired";

        public const string View = "CanViewInventory";
        public const string OpeningStock = "CanAddOpeningStock";
        public const string Adjust = "CanAdjustStock";
        public const string Approve = "CanApproveInventoryOperations";
        public const string Transfer = "CanTransferStock";
        public const string Receive = "CanReceiveStock";
        public const string ManageThresholds = "CanManageStockThresholds";

        public const string Reserve = "CanReserveStock";
        public const string CommitReservation = "CanCommitStockReservation";
        public const string RestockReturn = "CanRestockReturn";
    }
}
