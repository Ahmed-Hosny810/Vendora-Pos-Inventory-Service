using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockMovements.DTOS;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockMovements.Queries.GetMovementsHistoryQuery
{
    public class GetStockMovementsHistory:IRequest<PagedResponse<IEnumerable<StockMovementDto>>>
    {
        public GetStockMovementsHistoryParameter Parameter { get; set; } = new();
    }

    public class GetStockMovementsHistoryHandler : IRequestHandler<GetStockMovementsHistory, PagedResponse<IEnumerable<StockMovementDto>>>
    {
        private readonly IStockMovementRepository _stockMovementRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetStockMovementsHistoryHandler(
            IStockMovementRepository stockMovementRepository,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _stockMovementRepository = stockMovementRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }
        public async Task<PagedResponse<IEnumerable<StockMovementDto>>> Handle(GetStockMovementsHistory request, CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("TenantId claim is missing or invalid.");
            }

            var movements = await _stockMovementRepository.GetMovementsHistoryPagedAsync(tenantId.Value, request.Parameter.Filter,
                request.Parameter.OrderKey, request.Parameter.OrderDescending, request.Parameter.PageNumber, request.Parameter.PageSize, cancellationToken);

            var dto = _mapper.Map<IEnumerable<StockMovementDto>>(movements.Data);

            return new PagedResponse<IEnumerable<StockMovementDto>>(
                dto,
                movements.PageNumber,
                movements.PageSize,
                movements.TotalCount);
        }
    }

}
