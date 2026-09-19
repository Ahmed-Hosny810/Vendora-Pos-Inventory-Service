using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockAdjustments.DTOS;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;


namespace Pos.InventoryService.Application.Features.StockAdjustments.Queries.GetDetails
{
    public class GetStockAdjustmentDetailsQuery: IRequest<Result<StockAdjustmentDetailsDto>>
    {
        public Guid AdjustmentId { get; set; }
    }

    public class GetStockAdjustmentDetailsQueryHandler: IRequestHandler<GetStockAdjustmentDetailsQuery,Result<StockAdjustmentDetailsDto>>
    {
        private readonly IStockAdjustmentRepository _adjustments;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public GetStockAdjustmentDetailsQueryHandler(
            IStockAdjustmentRepository adjustments,
            ICurrentUserService currentUser,
            IMapper mapper)
        {
            _adjustments = adjustments;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        public async Task<Result<StockAdjustmentDetailsDto>> Handle(GetStockAdjustmentDetailsQuery request, CancellationToken cancellationToken)
        {
            var tenantId = _currentUser.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            var adjustment = await _adjustments.GetDetailsAsync(
                tenantId.Value,
                request.AdjustmentId,
                cancellationToken);

            if (adjustment == null)
            {
                return Result<StockAdjustmentDetailsDto>.Failure(
                    "Stock adjustment was not found.");
            }

            var dto = _mapper.Map<StockAdjustmentDetailsDto>(adjustment);

            return Result<StockAdjustmentDetailsDto>.Success(dto);
        }
    }
}
