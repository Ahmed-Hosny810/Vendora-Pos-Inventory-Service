using MediatR;
using Pos.InventoryService.Application.Features.StockAdjustments.DTOS;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.CreateDraft
{
    public class CreateStockAdjustmentDraftCommand : IRequest<Result<Guid>>
    {
        public Guid BranchId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<CreateStockAdjustmentItemDto> Items { get; set; } = new();
    }

    public class CreateStockAdjustmentDraftCommandHandler : IRequestHandler<CreateStockAdjustmentDraftCommand, Result<Guid>>
    {
        private readonly IStockAdjustmentRepository _adjustments;
        private readonly IStockBalanceRepositoryAsync _balances;
        private readonly IInventoryItemValidationService _itemValidation;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public CreateStockAdjustmentDraftCommandHandler(
            IStockAdjustmentRepository adjustments,
            IStockBalanceRepositoryAsync balances,
            IInventoryItemValidationService itemValidation,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork)
        {
            _adjustments = adjustments;
            _balances = balances;
            _itemValidation = itemValidation;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            CreateStockAdjustmentDraftCommand request,
            CancellationToken cancellationToken)
        {
            var tenantId = _currentUser.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            if (!Guid.TryParse(_currentUser.UserId, out var userId)
                || userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException(
                    "A valid user is required.");
            }

            var adjustmentId = Guid.NewGuid();

            var adjustment = new StockAdjustment
            {
                Id = adjustmentId,
                TenantId = tenantId.Value,
                BranchId = request.BranchId,
                AdjustmentNumber = $"ADJ-{adjustmentId:N}",
                Status = StockAdjustmentStatus.Draft,
                Reason = request.Reason.Trim(),
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in request.Items)
            {
                await _itemValidation.ValidateStockItemAsync(
                    tenantId.Value,
                    request.BranchId,
                    item.ProductId,
                    item.ProductVariantId,
                    item.NewQuantity,
                    cancellationToken);

                var balance =
                    await _balances.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: request.BranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                {
                    return Result<Guid>.Failure(
                        $"No stock balance exists for product " +
                        $"{item.ProductId} and variant {item.ProductVariantId}. " +
                        "Initialize its stock first.");
                }

                var delta = item.NewQuantity - balance.QuantityOnHand;

                if (delta < -999999999999999.999m ||
                    delta > 999999999999999.999m)
                {
                    return Result<Guid>.Failure(
                        "The quantity difference exceeds the supported range.");
                }

                adjustment.Items.Add(new StockAdjustmentItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    AdjustmentId = adjustment.Id,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,
                    OldQuantity = balance.QuantityOnHand,
                    NewQuantity = item.NewQuantity,
                    QuantityDelta = delta,
                    BalanceRowVersionAtCount = balance.RowVersion.ToArray()
                });
            }

            await _adjustments.AddAsync(adjustment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(adjustment.Id);
        }
    }
}
