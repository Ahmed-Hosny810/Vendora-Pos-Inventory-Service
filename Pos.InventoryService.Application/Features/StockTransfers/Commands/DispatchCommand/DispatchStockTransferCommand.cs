using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.DispatchCommand
{
    public class DispatchStockTransferCommand : IRequest<Result<Guid>>
    {
        public Guid TransferId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class DispatchStockTransferCommandHandler
        : IRequestHandler<DispatchStockTransferCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _stockTransferRepository;
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly IStockMovementRepository _stockMovementRepository;
        private readonly IInventoryItemValidationService _itemValidationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public DispatchStockTransferCommandHandler(
            IStockTransferRepositoryAsync stockTransferRepository,
            IStockBalanceRepositoryAsync stockBalanceRepository,
            IStockMovementRepository stockMovementRepository,
            IInventoryItemValidationService itemValidationService,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _stockTransferRepository = stockTransferRepository;
            _stockBalanceRepository = stockBalanceRepository;
            _stockMovementRepository = stockMovementRepository;
            _itemValidationService = itemValidationService;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            DispatchStockTransferCommand request,
            CancellationToken cancellationToken)
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

            // 2. Load the tracked transfer with its items.
            var transfer = await _stockTransferRepository.GetByIdAsync(
                tenantId.Value,
                request.TransferId,
                cancellationToken);

            if (transfer == null)
                return Result<Guid>.Failure(
                    "Stock transfer was not found.");

            // 3. A repeated dispatch must not deduct stock again.
            if (transfer.Status == StockTransferStatus.Dispatched ||
                transfer.Status == StockTransferStatus.PartiallyReceived ||
                transfer.Status == StockTransferStatus.Received)
            {
                return Result<Guid>.Success(transfer.Id);
            }

            // 4. Require an unchanged, approved transfer.
            if (transfer.Status != StockTransferStatus.Approved)
                return Result<Guid>.Failure(
                    "Only approved transfers can be dispatched.");

            if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This transfer has changed. Reload it before dispatching.");

            if (transfer.FromBranchId == transfer.ToBranchId)
                return Result<Guid>.Failure(
                    "Source and destination branches must be different.");

            if (transfer.Items == null || transfer.Items.Count == 0)
                return Result<Guid>.Failure(
                    "The transfer must contain at least one item.");

            var now = DateTime.UtcNow;

            foreach (var item in transfer.Items)
            {
                // 5. Check saved quantities and validate from branch
                if (item.Quantity <= 0)
                {
                    return Result<Guid>.Failure(
                        $"Transfer quantity for product {item.ProductId} must be positive.");
                }

                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value,
                    transfer.FromBranchId,
                    item.ProductId,
                    item.ProductVariantId,
                    item.Quantity,
                    cancellationToken);

                // 6. Load the tracked source balance and check availability.
                var balance =
                    await _stockBalanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: transfer.FromBranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                {
                    return Result<Guid>.Failure(
                        $"Source stock balance for product {item.ProductId} was not found.");
                }

                if (item.Quantity > balance.AvailableQuantity)
                {
                    return Result<Guid>.Failure(
                        $"Insufficient available stock for product {item.ProductId}.");
                }

                // 7. Deduct on-hand stock while leaving reservations unchanged.
                var beforeQuantity = balance.QuantityOnHand;

                balance.QuantityOnHand -= item.Quantity;
                balance.UpdatedAt = now;

                // 8. Record the goods leaving the source branch.
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    BranchId = transfer.FromBranchId,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,

                    MovementType = StockMovementType.TransferOut,
                    BeforeQty = beforeQuantity,
                    AfterQty = balance.QuantityOnHand,
                    QuantityDelta = -item.Quantity,

                    ReferenceType = StockReferenceType.StockTransfer,
                    ReferenceId = transfer.Id,

                    LowStockThreshold = balance.LowStockThreshold,
                    CreatedByUserId = userId,
                    CreatedAt = now
                };

                await _stockMovementRepository.AddAsync(
                    movement,
                    cancellationToken);
            }

            transfer.Status = StockTransferStatus.Dispatched;
            transfer.UpdatedAt = now;

            // 10. Save balances, movements and the transfer together.
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<Guid>.Failure(
                    "The transfer or its stock balances changed while dispatching. " +
                    "Reload the transfer and try again.");
            }

            return Result<Guid>.Success(transfer.Id);
        }
    }
}