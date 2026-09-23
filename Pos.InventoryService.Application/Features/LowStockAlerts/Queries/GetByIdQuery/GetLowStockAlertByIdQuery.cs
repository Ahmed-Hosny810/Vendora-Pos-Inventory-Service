using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.LowStockAlerts.DTOs;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetByIdQuery;

public class GetLowStockAlertByIdQuery : IRequest<Result<LowStockAlertDto>>
{
    public Guid AlertId { get; set; }
}

public class GetLowStockAlertByIdQueryHandler
    : IRequestHandler<GetLowStockAlertByIdQuery, Result<LowStockAlertDto>>
{
    private readonly ILowStockAlertRepositoryAsync _alerts;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetLowStockAlertByIdQueryHandler(
        ILowStockAlertRepositoryAsync alerts, ICurrentUserService currentUser, IMapper mapper)
    {
        _alerts = alerts;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<LowStockAlertDto>> Handle(
        GetLowStockAlertByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant is required.");

        var alert = await _alerts.GetByIdAsync(tenantId.Value, request.AlertId, cancellationToken);
        if (alert == null)
            return Result<LowStockAlertDto>.Failure("Low-stock alert was not found.");

        return Result<LowStockAlertDto>.Success(_mapper.Map<LowStockAlertDto>(alert));
    }
}
