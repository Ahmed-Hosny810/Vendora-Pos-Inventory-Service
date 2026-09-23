using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Application.Features.StockBalances.Commands.AddOpeningStock;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetProductStock;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetStockBatchQuey;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetBalancesQuery;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Features.StockBalances.Commands.UpdateThreshold;

namespace Pos.InventoryService.WebApi.Controllers.V1
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize(Policy = InventoryPolicies.TenantRequired)]
    public class StockBalancesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StockBalancesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        [Authorize(Policy = InventoryPolicies.View)]
        public async Task<IActionResult> GetAll(
            [FromQuery] GetStockBalancesQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("availability")]
        [Authorize(Policy = InventoryPolicies.View)]
        public async Task<IActionResult> GetAvailability(
            [FromQuery] GetProductStockByProductIdQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);

            if (result.IsFailure)
                return NotFound(new Response<StockBalanceDto>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<StockBalanceDto>(data: result.Value!));
        }

        [HttpPost("availability/batch")]
        [Authorize(Policy = InventoryPolicies.View)]
        public async Task<IActionResult> GetBatchAvailability(
            [FromBody] GetBatchStockAvailabilityQuery command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return BadRequest(new Response<IReadOnlyList<StockBalanceDto>>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<IReadOnlyList<StockBalanceDto>>(data: result.Value!));
        }

        [HttpPut("threshold")]
        [Authorize(Policy = InventoryPolicies.ManageThresholds)]
        public async Task<IActionResult> UpdateThreshold(
            [FromBody] UpdateStockThresholdCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }

        [HttpPost("opening-stock")]
        [Authorize(Policy = InventoryPolicies.OpeningStock)]
        public async Task<IActionResult> AddOpeningStock(
            [FromBody] AddOpeningStockCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsFailure)
                return Conflict(new Response<Guid>(message: string.Join(", ", result.Errors)));

            return Ok(new Response<Guid>(data: result.Value));
        }
    }
}
