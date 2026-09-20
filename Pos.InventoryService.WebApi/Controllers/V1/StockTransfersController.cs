using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.CreateDraftCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.UpdateDraftCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.RequestCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.ApproveCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.CancelCommand;
using Pos.InventoryService.Application.Features.StockTransfers.DTOS;
using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery;
using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetDetails;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.WebApi.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Policy = InventoryPolicies.TenantRequired)]
    public class StockTransfersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StockTransfersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [Authorize(Policy = InventoryPolicies.Transfer)]
        public async Task<ActionResult<Response<Guid>>> CreateDraft(
            [FromBody] CreateStockTransferDraftCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return BadRequest(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return CreatedAtAction(
                nameof(GetDetails),
                new { version = "1", transferId = result.Value },
                new Response<Guid>(data: result.Value, message: "Stock transfer draft created."));
        }

        [HttpPut]
        [Authorize(Policy = InventoryPolicies.Transfer)]
        public async Task<ActionResult<Response<Guid>>> UpdateDraft(
            [FromBody] UpdateStockTransferDraftCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("request")]
        [Authorize(Policy = InventoryPolicies.Transfer)]
        public async Task<ActionResult<Response<Guid>>> SubmitRequest(
            [FromBody] RequestStockTransferCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("approve")]
        [Authorize(Policy = InventoryPolicies.Approve)]
        public async Task<ActionResult<Response<Guid>>> Approve(
            [FromBody] ApproveStockTransferCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("cancel")]
        [Authorize(Policy = InventoryPolicies.Approve)]
        public async Task<ActionResult<Response<Guid>>> Cancel(
            [FromBody] CancelStockTransferCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpGet]
        [Authorize(Policy = InventoryPolicies.View)]
        public async Task<ActionResult<PagedResponse<IEnumerable<StockTransferDto>>>> GetAll(
            [FromQuery] GetStockTransfersQuery query,
            CancellationToken cancellationToken)
        {
            return Ok(await _mediator.Send(query, cancellationToken));
        }

        [HttpGet("{transferId:guid}")]
        [Authorize(Policy = InventoryPolicies.View)]
        public async Task<ActionResult<Response<StockTransferDetailsDto>>> GetDetails(
            Guid transferId,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new GetStockTransferDetailsQuery { TransferId = transferId },
                cancellationToken);

            if (result.IsFailure)
                return NotFound(new Response<StockTransferDetailsDto>(
                    message: string.Join(", ", result.Errors)));

            return Ok(new Response<StockTransferDetailsDto>(data: result.Value!));
        }
    }
}
