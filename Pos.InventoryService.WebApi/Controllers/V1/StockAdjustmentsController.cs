using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.CreateDraft;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.UpdateDraft;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.ApproveAdjustmentCommand;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.CancelAdjustmentCommand;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.PostAdjustmentCommand;
using Pos.InventoryService.Application.Features.StockAdjustments.Queries.GetDetails;
using Pos.InventoryService.Application.Features.StockAdjustments.DTOS;

namespace Pos.InventoryService.WebApi.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Policy = InventoryPolicies.TenantRequired)]
    public class StockAdjustmentsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StockAdjustmentsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{adjustmentId:guid}")]
        [Authorize(Policy = InventoryPolicies.View)]
        public async Task<IActionResult> GetDetails(Guid adjustmentId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new GetStockAdjustmentDetailsQuery { AdjustmentId = adjustmentId }, cancellationToken);

            if (result.IsFailure)
                return NotFound(new Response<StockAdjustmentDetailsDto>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<StockAdjustmentDetailsDto>(data: result.Value!));
        }

        [HttpPost]
        [Authorize(Policy = InventoryPolicies.Adjust)]
        public async Task<IActionResult> CreateDraft(
            [FromBody] CreateStockAdjustmentDraftCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPut]
        [Authorize(Policy = InventoryPolicies.Adjust)]
        public async Task<IActionResult> UpdateDraft(
            [FromBody] UpdateStockAdjustmentDraftCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("approve")]
        [Authorize(Policy = InventoryPolicies.Approve)]
        public async Task<IActionResult> Approve(
            [FromBody] ApproveStockAdjustmentCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<string>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<string>(data: "Operation completed successfully."));
        }

        [HttpPost("cancel")]
        [Authorize(Policy = InventoryPolicies.Approve)]
        public async Task<IActionResult> Cancel(
            [FromBody] CancelStockAdjustmentCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<string>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<string>(data: "Operation completed successfully."));
        }

        [HttpPost("post")]
        [Authorize(Policy = InventoryPolicies.Approve)]
        public async Task<IActionResult> Post(
            [FromBody] PostStockAdjustmentCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<string>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<string>(data: "Operation completed successfully."));
        }
    }
}
