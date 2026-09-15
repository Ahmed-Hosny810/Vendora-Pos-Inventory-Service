using Pos.InventoryService.Application.Features.StockBalance.DTOs;
using Pos.InventoryService.Application.Features.StockBalance.Queries.GetBalancesQuery;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IStockBalanceRepositoryAsync:IGenericRepositoryAsync<StockBalance,Guid>
    {
        Task<StockBalance?> GetProductStockBalanceAsync(Guid tenantId, Guid branchId, Guid productId,Guid? productVariantId, CancellationToken cancellationToken);

        Task<IReadOnlyList<StockBalance>> GetBatchStockAvailabilityAsync(Guid tenantId,Guid branchId , List<StockAvailabilityItem> items, CancellationToken cancellationToken);

        Task<PagedResponse<IEnumerable<StockBalance>>> GetStockBalancesPagedResponseAsync(
            Guid tenantId,
            StockBalanceFilter? filter,
            StockBalanceOrderKey orderKey,
            bool orderDescending,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken);
    }
}
