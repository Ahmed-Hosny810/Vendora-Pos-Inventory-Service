using Asp.Versioning;
using Pos.InventoryService.Application.Features.StockReservations.Queries.GetByIdQuery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Application.Features.StockReservations.Commands.CreateCommand;
using Pos.InventoryService.Application.Features.StockReservations.Commands.ConsumeCommand;
using Pos.InventoryService.Application.Features.StockReservations.Commands.ReleaseCommand;

namespace Pos.InventoryService.WebApi.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Policy = InventoryPolicies.TenantRequired)]
    public class StockReservationsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StockReservationsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = InventoryPolicies.Reserve)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetStockReservationByIdQuery
            {
                ReservationId = id
            }, cancellationToken);
            return result.IsSuccess
                ? Ok(new Response<StockReservationDetailsDto>(data: result.Value!))
                : NotFound(new Response<StockReservationDetailsDto>(message: string.Join(", ", result.Errors)));
        }
        [HttpPost]
        [Authorize(Policy = InventoryPolicies.Reserve)]
        public async Task<IActionResult> Create(
            [FromBody] CreateStockReservationCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("consume")]
        [Authorize(Policy = InventoryPolicies.CommitReservation)]
        public async Task<IActionResult> Consume(
            [FromBody] ConsumeStockReservationCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("release")]
        [Authorize(Policy = InventoryPolicies.Reserve)]
        public async Task<IActionResult> Release(
            [FromBody] ReleaseStockReservationCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<string>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<string>(data: "Operation completed successfully."));
        }
    }
}

