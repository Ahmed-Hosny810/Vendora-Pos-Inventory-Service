using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;


namespace Pos.InventoryService.Application.Features.StockBalances.Queries.GetProductStock
{
    public class GetProductStockByProductIdQuery:IRequest<Result<StockBalanceDto>>
    {
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
    }
    public class GetProductStockByProductIdQueryHandler : IRequestHandler<GetProductStockByProductIdQuery, Result<StockBalanceDto>>
    {
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetProductStockByProductIdQueryHandler(
            IStockBalanceRepositoryAsync stockBalanceRepository,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _stockBalanceRepository = stockBalanceRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }
        public async Task<Result<StockBalanceDto>> Handle(GetProductStockByProductIdQuery request, CancellationToken cancellationToken)
        {
            var tenantId=_currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                return Result<StockBalanceDto>.Failure("TenantId claim is missing.");

            var stockBalance = await _stockBalanceRepository.GetProductStockBalanceAsync(tenantId.Value, request.BranchId,
                request.ProductId, request.ProductVariantId ,cancellationToken);

            if (stockBalance == null)
                return Result<StockBalanceDto>.Failure($"Stock balance for  product with Id {request.ProductId} not found.");

            var stockBalanceDto=_mapper.Map<StockBalanceDto>(stockBalance);

            return Result<StockBalanceDto>.Success(stockBalanceDto);
        }
    }
}
