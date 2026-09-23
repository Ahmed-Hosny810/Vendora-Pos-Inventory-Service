using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.QueryExtensions;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories;

public class LowStockAlertRepositoryAsync
    : GenericRepositoryAsync<LowStockAlert, Guid>, ILowStockAlertRepositoryAsync
{
    private readonly ApplicationDbContext _context;

    public LowStockAlertRepositoryAsync(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public Task<LowStockAlert?> GetByIdAsync(
        Guid tenantId, Guid alertId, CancellationToken cancellationToken)
    {
        return _context.LowStockAlerts.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == alertId, cancellationToken);
    }

    public async Task<PagedResponse<IEnumerable<LowStockAlert>>> GetPagedAsync(
        Guid tenantId,
        LowStockAlertFilter? filter,
        LowStockAlertOrderKey orderKey,
        bool orderDescending,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 50);

        var query = _context.LowStockAlerts.AsNoTracking()
            .ApplyFilters(tenantId, filter);

        var totalCount = await query.CountAsync(cancellationToken);
        var alerts = await query
            .ApplyOrdering(orderKey, orderDescending)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<IEnumerable<LowStockAlert>>(
            alerts, pageNumber, pageSize, totalCount);
    }
}
