using MediatR;
using Pos.InventoryService.Application.Features.StockTransfers.DTOs;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.CreateDraftCommand
{
    public class CreateStockTransferDraftCommand:IRequest<Result<Guid>>
    {
        public Guid FromBranchId { get; set; }
        public Guid ToBranchId { get; set; }
        public List<StockTransferItemDto> Items { get; set; } = new();
    }

    public class CreateStockTransferDraftCommandHandler : IRequestHandler<CreateStockTransferDraftCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _stockTransferRepository;
        private readonly IInventoryItemValidationService _itemValidationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateStockTransferDraftCommandHandler(
            IStockTransferRepositoryAsync stockTransferRepository,
            IInventoryItemValidationService itemValidationService,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _stockTransferRepository = stockTransferRepository;
            _itemValidationService = itemValidationService;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }
        public async Task<Result<Guid>> Handle(CreateStockTransferDraftCommand request, CancellationToken cancellationToken)
        {
            // 1. Get the authenticated tenant and user.
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            if (!Guid.TryParse(_currentUserService.UserId, out var userId)
                || userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException(
                    "A valid user is required.");
            }

            // 2. Create the draft without reserving or moving stock.
            var transferId = Guid.NewGuid();

            var transfer = new StockTransfer
            {
                Id = transferId,
                TenantId = tenantId.Value,
                TransferNumber = $"TRF-{transferId:N}",
                FromBranchId = request.FromBranchId,
                ToBranchId = request.ToBranchId,
                Status = StockTransferStatus.Draft,
                RequestedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in request.Items)
            {
                // 3. Validate both branches and the product/variant/unit rules.
                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value,
                    request.FromBranchId,
                    item.ProductId,
                    item.ProductVariantId,
                    item.Quantity,
                    cancellationToken);

                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value,
                    request.ToBranchId,
                    item.ProductId,
                    item.ProductVariantId,
                    item.Quantity,
                    cancellationToken);

                // 4. Add the requested quantity;
                transfer.Items.Add(new StockTransferItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    TransferId = transfer.Id,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity,
                    ReceivedQuantity = 0
                });
            }

            // 5. Save the transfer and its items together.
            await _stockTransferRepository.AddAsync(transfer, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(transfer.Id);
        }
    }
}
