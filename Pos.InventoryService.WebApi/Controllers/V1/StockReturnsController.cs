using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.WebApi.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = InventoryPolicies.RestockReturn)]
public class StockReturnsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockReturnsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Response<Guid>>> Restock(
        [FromBody] RestockCustomerReturnCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

        return Ok(new Response<Guid>(
            data: result.Value, message: "Return stock processing completed."));
    }
}

