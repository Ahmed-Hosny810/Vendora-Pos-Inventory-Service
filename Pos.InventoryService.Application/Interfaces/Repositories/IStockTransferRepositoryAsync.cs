using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IStockTransferRepositoryAsync:IGenericRepositoryAsync<StockTransfer,Guid>
    {
        Task<StockTransfer?> GetByIdAsync(
            Guid tenantId,
            Guid transferId,
            CancellationToken cancellationToken);

        Task<PagedResponse<IEnumerable<StockTransfer>>> GetPagedAsync(
            Guid tenantId,
            StockTransferFilter? filter,
            StockTransferOrderKey orderKey,
            bool orderDescending,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken);

        void RemoveItems(
            IEnumerable<StockTransferItem> items);
    }
}
