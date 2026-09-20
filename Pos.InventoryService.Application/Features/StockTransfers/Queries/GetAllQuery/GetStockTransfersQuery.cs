using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockTransfers.DTOS;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery
{
    public class GetStockTransfersQuery : IRequest<PagedResponse<IEnumerable<StockTransferDto>>>
    {
        public GetStockTransfersQueryParameter Parameter { get; set; } = new();
    }

    public class GetStockTransfersQueryHandler
        : IRequestHandler<GetStockTransfersQuery, PagedResponse<IEnumerable<StockTransferDto>>>
    {
        private readonly IStockTransferRepositoryAsync _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public GetStockTransfersQueryHandler(
            IStockTransferRepositoryAsync repository,
            ICurrentUserService currentUser,
            IMapper mapper)
        {
            _repository = repository;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        public async Task<PagedResponse<IEnumerable<StockTransferDto>>> Handle(
            GetStockTransfersQuery request,
            CancellationToken cancellationToken)
        {
            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException("A valid tenant is required.");

            var parameter = request.Parameter;

            var transfers = await _repository.GetPagedAsync(
                tenantId.Value,
                parameter.Filter,
                parameter.OrderKey,
                parameter.OrderDescending,
                parameter.PageNumber,
                parameter.PageSize,
                cancellationToken);

            var dto = _mapper.Map<IEnumerable<StockTransferDto>>(transfers.Data);

            return new PagedResponse<IEnumerable<StockTransferDto>>(
                dto,
                transfers.PageNumber,
                transfers.PageSize,
                transfers.TotalCount);
        }
    }
}
