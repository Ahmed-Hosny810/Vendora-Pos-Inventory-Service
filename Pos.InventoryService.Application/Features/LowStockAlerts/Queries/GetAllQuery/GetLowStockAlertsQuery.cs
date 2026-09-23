using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.LowStockAlerts.DTOs;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;

public class GetLowStockAlertsQuery : IRequest<PagedResponse<IEnumerable<LowStockAlertDto>>>
{
    public GetLowStockAlertsQueryParameter Parameter { get; set; } = new();
}

public class GetLowStockAlertsQueryHandler
    : IRequestHandler<GetLowStockAlertsQuery, PagedResponse<IEnumerable<LowStockAlertDto>>>
{
    private readonly ILowStockAlertRepositoryAsync _alerts;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetLowStockAlertsQueryHandler(
        ILowStockAlertRepositoryAsync alerts, ICurrentUserService currentUser, IMapper mapper)
    {
        _alerts = alerts;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<PagedResponse<IEnumerable<LowStockAlertDto>>> Handle(
        GetLowStockAlertsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant is required.");

        var alerts = await _alerts.GetPagedAsync(
            tenantId.Value, request.Parameter.Filter,
            request.Parameter.OrderKey, request.Parameter.OrderDescending,
            request.Parameter.PageNumber, request.Parameter.PageSize, cancellationToken);
        var data = _mapper.Map<IEnumerable<LowStockAlertDto>>(alerts.Data);

        return new PagedResponse<IEnumerable<LowStockAlertDto>>(
            data, alerts.PageNumber, alerts.PageSize, alerts.TotalCount);
    }
}
