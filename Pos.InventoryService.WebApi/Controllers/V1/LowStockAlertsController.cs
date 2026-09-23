using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Features.LowStockAlerts.DTOs;
using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;
using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetByIdQuery;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.WebApi.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = InventoryPolicies.TenantRequired)]
[Authorize(Policy = InventoryPolicies.View)]
public class LowStockAlertsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LowStockAlertsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetLowStockAlertsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetLowStockAlertByIdQuery { AlertId = id }, cancellationToken);

        if (result.IsFailure)
            return NotFound(new Response<LowStockAlertDto>(
                message: string.Join(", ", result.Errors)));

        return Ok(new Response<LowStockAlertDto>(data: result.Value!));
    }
}
