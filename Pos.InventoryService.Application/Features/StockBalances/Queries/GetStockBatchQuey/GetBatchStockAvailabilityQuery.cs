using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Features.StockBalances.Queries.GetStockBatchQuey
{
    public class GetBatchStockAvailabilityQuery:IRequest<Result<IReadOnlyList<StockBalanceDto>>>
    {
        public Guid BranchId { get; set; }
        public List<StockAvailabilityItem> Items { get; set; } = new();
    }
    public class GetBatchStockAvailabilityQueryHandler : IRequestHandler<GetBatchStockAvailabilityQuery, Result<IReadOnlyList<StockBalanceDto>>>
    {
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetBatchStockAvailabilityQueryHandler(
            IStockBalanceRepositoryAsync stockBalanceRepository,
            ICurrentUserService currentUserService,
            IMapper mapper)
        {
            _stockBalanceRepository = stockBalanceRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;
        }
        public async Task<Result<IReadOnlyList<StockBalanceDto>>> Handle(GetBatchStockAvailabilityQuery request, CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return Result<IReadOnlyList<StockBalanceDto>>
                    .Failure("TenantId claim is missing or invalid.");
            }

            var items = request.Items
                .DistinctBy(item => new
                {
                    item.ProductId,
                    item.ProductVariantId
                })
                .ToList();

            var balances =
                await _stockBalanceRepository.GetBatchStockAvailabilityAsync(
                    tenantId: tenantId.Value,
                    branchId: request.BranchId,
                    items: items,
                    cancellationToken: cancellationToken);

            var dtos = _mapper.Map<List<StockBalanceDto>>(balances);

            return Result<IReadOnlyList<StockBalanceDto>>.Success(dtos);
        }
    }
}
