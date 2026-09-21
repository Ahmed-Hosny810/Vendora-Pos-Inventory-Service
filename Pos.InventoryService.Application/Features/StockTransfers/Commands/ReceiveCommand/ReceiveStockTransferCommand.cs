using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockTransfers.DTOs;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.ReceiveCommand
{
    public class ReceiveStockTransferCommand : IRequest<Result<Guid>>
    {
        public Guid TransferId { get; set; }

        public Guid IdempotencyKey { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public List<ReceiveStockTransferItemDto> Items { get; set; } = new();
    }

    public class ReceiveStockTransferCommandHandler
        : IRequestHandler<ReceiveStockTransferCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _transferRepository;
        private readonly IStockBalanceRepositoryAsync _balanceRepository;
        private readonly IStockMovementRepository _movementRepository;
        private readonly IInventoryItemValidationService _itemValidationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public ReceiveStockTransferCommandHandler(
            IStockTransferRepositoryAsync transferRepository,
            IStockBalanceRepositoryAsync balanceRepository,
            IStockMovementRepository movementRepository,
            IInventoryItemValidationService itemValidationService,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _transferRepository = transferRepository;
            _balanceRepository = balanceRepository;
            _movementRepository = movementRepository;
            _itemValidationService = itemValidationService;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            ReceiveStockTransferCommand request,
            CancellationToken cancellationToken)
        {
            // 1. Get the authenticated tenant and receiving user.
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            if (!Guid.TryParse(_currentUserService.UserId, out var userId) ||
                userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException(
                    "A valid user is required.");
            }

            // 2. Load the tracked transfer with its items.
            var transfer = await _transferRepository.GetByIdAsync(
                tenantId.Value,
                request.TransferId,
                cancellationToken);

            if (transfer == null)
                return Result<Guid>.Failure(
                    "Stock transfer was not found.");

            // 3. Return success if this receipt was already processed.
            var alreadyReceived =
                await _movementRepository.TransferReceiptExistsAsync(
                    tenantId.Value,
                    transfer.Id,
                    request.IdempotencyKey,
                    cancellationToken);

            if (alreadyReceived)
                return Result<Guid>.Success(transfer.Id);

            // 4. Check the transfer status and row version.
            if (transfer.Status != StockTransferStatus.Dispatched &&
                transfer.Status != StockTransferStatus.PartiallyReceived)
            {
                return Result<Guid>.Failure(
                    "Only dispatched or partially received transfers can receive stock.");
            }

            if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This transfer has changed. Reload it before receiving.");

            if (transfer.Items == null || transfer.Items.Count == 0)
                return Result<Guid>.Failure(
                    "The transfer has no items.");

            if (transfer.FromBranchId == transfer.ToBranchId)
                return Result<Guid>.Failure(
                    "Source and destination branches must be different.");

            var now = DateTime.UtcNow;

            foreach (var input in request.Items)
            {
                // 5. Find the transfer item and check its outstanding quantity.
                var item = transfer.Items.SingleOrDefault(
                    x => x.Id == input.TransferItemId);

                if (item == null)
                {
                    return Result<Guid>.Failure(
                        $"Item {input.TransferItemId} does not belong to this transfer.");
                }

                var remainingQuantity =
                    item.Quantity - item.ReceivedQuantity;

                if (input.Quantity > remainingQuantity)
                {
                    return Result<Guid>.Failure(
                        $"Received quantity for product {item.ProductId} " +
                        "exceeds its outstanding quantity.");
                }

                // 6. Validate the destination, product, variant and unit rules.
                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value,
                    transfer.ToBranchId,
                    item.ProductId,
                    item.ProductVariantId,
                    input.Quantity,
                    cancellationToken);

                // 7. Load or create the destination stock balance.
                var balance =
                    await _balanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: transfer.ToBranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                {
                    balance = new StockBalance
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId.Value,
                        BranchId = transfer.ToBranchId,
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        QuantityOnHand = 0,
                        QuantityReserved = 0,
                        LowStockThreshold = 0,
                        CreatedAt = now
                    };

                    await _balanceRepository.AddAsync(
                        balance,
                        cancellationToken);
                }

                // 8. Check that the new stock quantity fits decimal(18,3).
                const decimal maxQuantity = 999999999999999.999m;

                if (balance.QuantityOnHand > maxQuantity - input.Quantity)
                {
                    return Result<Guid>.Failure(
                        $"Stock quantity for product {item.ProductId} " +
                        "would exceed the supported range.");
                }

                // 9. Add only the quantity received in this request.
                var beforeQuantity = balance.QuantityOnHand;

                balance.QuantityOnHand += input.Quantity;
                balance.UpdatedAt = now;

                item.ReceivedQuantity += input.Quantity;

                // 10. Record the receipt movement and its idempotency key.
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    BranchId = transfer.ToBranchId,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,

                    MovementType = StockMovementType.TransferIn,
                    BeforeQty = beforeQuantity,
                    AfterQty = balance.QuantityOnHand,
                    QuantityDelta = input.Quantity,

                    ReferenceType = StockReferenceType.StockTransfer,
                    ReferenceId = transfer.Id,
                    IdempotencyKey = request.IdempotencyKey,

                    LowStockThreshold = balance.LowStockThreshold,
                    CreatedByUserId = userId,
                    CreatedAt = now
                };

                await _movementRepository.AddAsync(
                    movement,
                    cancellationToken);
            }

            // 11. Update the cumulative receipt status.
            var fullyReceived = transfer.Items.All(
                item => item.ReceivedQuantity == item.Quantity);

            transfer.Status = fullyReceived
                ? StockTransferStatus.Received
                : StockTransferStatus.PartiallyReceived;

            if (fullyReceived)
                transfer.ReceivedAt = now;

            transfer.UpdatedAt = now;

            // 12. Save balances, items, movements and transfer status together.
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<Guid>.Failure(
                    "The transfer or destination stock changed while receiving. " +
                    "Reload the transfer and retry using the same idempotency key.");
            }
            catch (DuplicateStockWriteException)
            {
                return Result<Guid>.Failure(
                    "A stock write conflicted with another request. " +
                    "Reload the transfer and retry using the same idempotency key.");
            }

            return Result<Guid>.Success(transfer.Id);
        }
    }
}
