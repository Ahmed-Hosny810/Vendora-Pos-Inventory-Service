using Pos.InventoryService.Application.Parameters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pos.InventoryService.Application.Features.StockBalance.Queries.GetBalancesQuery
{
    public class GetStockBalancesQueryParameter: RequestParameter<StockBalanceOrderKey>
    {
        public StockBalanceFilter? Filter { get; set; }
    }

    public class StockBalanceFilter
    {
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public bool LowStockOnly { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal? QuantityOnHand { get; set; }
        public decimal? QuantityReserved { get; set; }
        public decimal? LowStockThreshold { get; set; }

    }

    public enum StockBalanceOrderKey
    {
        CreatedAt,
        UpdatedAt,
        QuantityOnHand,
        LowStockThreshold

    }
}
