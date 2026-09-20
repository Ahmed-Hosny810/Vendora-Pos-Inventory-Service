using AutoMapper;
using MediatR;
using Pos.InventoryService.Application.Features.StockTransfers.DTOS;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockTransfers.Queries.GetDetails
{
    public class GetStockTransferDetailsQuery : IRequest<Result<StockTransferDetailsDto>>
    {
        public Guid TransferId { get; set; }
    }

    public class GetStockTransferDetailsQueryHandler : IRequestHandler<GetStockTransferDetailsQuery, Result<StockTransferDetailsDto>>
    {
        private readonly IStockTransferRepositoryAsync _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        public GetStockTransferDetailsQueryHandler(
            IStockTransferRepositoryAsync repository,
            ICurrentUserService currentUser,
            IMapper mapper)
        {
            _repository = repository;
            _currentUser = currentUser;
            _mapper = mapper;
        }

        public async Task<Result<StockTransferDetailsDto>> Handle(
            GetStockTransferDetailsQuery request,
            CancellationToken cancellationToken)
        {
            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException("A valid tenant is required.");

            var transfer = await _repository.GetByIdAsync(
                tenantId.Value, request.TransferId, cancellationToken);

            if (transfer == null)
                return Result<StockTransferDetailsDto>.Failure("Stock transfer was not found.");

            var dto = _mapper.Map<StockTransferDetailsDto>(transfer);

            return Result<StockTransferDetailsDto>.Success(dto);
        }
    }
}

