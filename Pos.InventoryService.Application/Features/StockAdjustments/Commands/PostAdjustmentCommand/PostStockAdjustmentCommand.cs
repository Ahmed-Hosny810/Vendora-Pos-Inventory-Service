using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.PostAdjustmentCommand
{
    public class PostStockAdjustmentCommand : IRequest<Result>
    {
        public Guid AdjustmentId { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class PostStockAdjustmentCommandHandler : IRequestHandler<PostStockAdjustmentCommand, Result>
    {
        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly IStockMovementRepository _stockMovementRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public PostStockAdjustmentCommandHandler(
            IStockAdjustmentRepository stockAdjustmentRepository,
            IStockBalanceRepositoryAsync stockBalanceRepository,
            IStockMovementRepository stockMovementRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository;
            _stockBalanceRepository = stockBalanceRepository;
            _stockMovementRepository = stockMovementRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(
            PostStockAdjustmentCommand request,
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

            // 2. Load the tracked adjustment with its items.
            var adjustment =
                await _stockAdjustmentRepository.GetForUpdateAsync(
                    tenantId.Value,
                    request.AdjustmentId,
                    cancellationToken);

            if (adjustment == null)
                return Result.Failure(
                    "Stock adjustment was not found.");

            // 3. Prevent a repeated request from posting stock twice.
            if (adjustment.Status == StockAdjustmentStatus.Posted)
                return Result.Success();

            // 4. Check the adjustment status, version and items.
            if (adjustment.Status != StockAdjustmentStatus.Approved)
                return Result.Failure(
                    "Only approved adjustments can be posted.");

            if (!adjustment.RowVersion.SequenceEqual(request.RowVersion))
                return Result.Failure(
                    "This adjustment has changed. Reload it before posting.");

            if (adjustment.Items == null || adjustment.Items.Count == 0)
                return Result.Failure(
                    "An adjustment must contain at least one item.");

            var now = DateTime.UtcNow;

            foreach (var item in adjustment.Items)
            {
                // 5. Load the tracked balance for this item.
                var balance =
                    await _stockBalanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: adjustment.BranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                    return Result.Failure(
                        $"Stock balance for product {item.ProductId} was not found.");

                if (!balance.RowVersion.SequenceEqual(item.BalanceRowVersionAtCount))
                {
                    return Result.Failure(
                        $"Stock for product {item.ProductId} has changed " +
                        "since counting. Cancel this adjustment and create " +
                        "a new one with a fresh count.");
                }

                // 7. Ensure the counted quantity covers existing reservations.
                if (item.NewQuantity < balance.QuantityReserved)
                {
                    return Result.Failure(
                        $"Counted quantity for product {item.ProductId} " +
                        "cannot be less than its reserved quantity.");
                }

                // 8. Calculate the actual change and skip unchanged quantities.
                var beforeQuantity = balance.QuantityOnHand;
                var delta = item.NewQuantity - beforeQuantity;

                if (delta == 0)
                    continue;

                // 9. Record the stock change and its source adjustment.
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    BranchId = adjustment.BranchId,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,

                    MovementType = StockMovementType.Adjustment,
                    BeforeQty = beforeQuantity,
                    AfterQty = item.NewQuantity,
                    QuantityDelta = delta,

                    ReferenceType = StockReferenceType.StockAdjustment,
                    ReferenceId = adjustment.Id,

                    LowStockThreshold = balance.LowStockThreshold,
                    CreatedByUserId = userId,
                    CreatedAt = now
                };

                await _stockMovementRepository.AddAsync(
                    movement,
                    cancellationToken);

                // 10. Update on-hand stock, keeping reservations unchanged.
                balance.QuantityOnHand = item.NewQuantity;
                balance.UpdatedAt = now;
            }

            // 11. Mark the adjustment as posted.
            adjustment.Status = StockAdjustmentStatus.Posted;
            adjustment.PostedAt = now;
            adjustment.UpdatedAt = now;

            // 12. Save balances, movements and adjustment status together.
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure(
                    "The adjustment or its stock balances changed while " +
                    "posting. Reload the adjustment and review the stock.");
            }

            return Result.Success();
        }
    }
}