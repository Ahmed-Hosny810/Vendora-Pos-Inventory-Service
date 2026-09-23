using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories;

public interface ILowStockAlertRepositoryAsync : IGenericRepositoryAsync<LowStockAlert, Guid>
{
    Task<LowStockAlert?> GetByIdAsync(Guid tenantId, Guid alertId, CancellationToken cancellationToken);
    Task<PagedResponse<IEnumerable<LowStockAlert>>> GetPagedAsync(
        Guid tenantId,
        LowStockAlertFilter? filter,
        LowStockAlertOrderKey orderKey,
        bool orderDescending,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}
