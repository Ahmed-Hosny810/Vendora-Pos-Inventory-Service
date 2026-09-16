using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;


namespace Pos.InventoryService.Application.Features.StockBalances.Queries.GetBalancesQuery
{
    public class GetStockBalancesQuery:IRequest<PagedResponse<IEnumerable<StockBalanceDto>>>
    {
        public GetStockBalancesQueryParameter Parameter { get; set; } = new();
    }
    public class GetStockBalancesQueryHandler : IRequestHandler<GetStockBalancesQuery, PagedResponse<IEnumerable<StockBalanceDto>>>
    {
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly IMapper _mapper;
        private readonly ICurrentUserService _currentUserService;

        public GetStockBalancesQueryHandler(
            IStockBalanceRepositoryAsync stockBalanceRepository,
            IMapper mapper,
            ICurrentUserService currentUserService)
        {
            _stockBalanceRepository = stockBalanceRepository;
            _mapper = mapper;
            _currentUserService = currentUserService;
        }
        public async Task<PagedResponse<IEnumerable<StockBalanceDto>>> Handle(GetStockBalancesQuery request, CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("TenantId claim is missing or invalid.");
            }

            var stockBalances = await _stockBalanceRepository.GetStockBalancesPagedResponseAsync(tenantId.Value, request.Parameter.Filter, 
                request.Parameter.OrderKey, request.Parameter.OrderDescending,request.Parameter.PageNumber, request.Parameter.PageSize, cancellationToken);

            var dto = _mapper.Map<IEnumerable<StockBalanceDto>>(stockBalances.Data);

            return new PagedResponse<IEnumerable<StockBalanceDto>>(
                dto,
                stockBalances.PageNumber,
                stockBalances.PageSize,
                stockBalances.TotalCount);
        }
    }
}
