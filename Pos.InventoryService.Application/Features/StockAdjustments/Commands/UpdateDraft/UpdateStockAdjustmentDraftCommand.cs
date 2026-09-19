using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockAdjustments.DTOS;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.UpdateDraft
{
    public class UpdateStockAdjustmentDraftCommand : IRequest<Result<Guid>>
    {
        public Guid AdjustmentId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public List<UpdateStockAdjustmentItemDto> Items { get; set; } = new();
    }

    public class UpdateStockAdjustmentDraftCommandHandler: IRequestHandler<UpdateStockAdjustmentDraftCommand, Result<Guid>>
    {
        private readonly IStockAdjustmentRepository _adjustmentsRepository;
        private readonly IStockBalanceRepositoryAsync _balancesRepository;
        private readonly IInventoryItemValidationService _itemValidation;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateStockAdjustmentDraftCommandHandler(
            IStockAdjustmentRepository adjustmentsRepository,
            IStockBalanceRepositoryAsync balancesRepository,
            IInventoryItemValidationService itemValidation,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork)
        {
            _adjustmentsRepository = adjustmentsRepository;
            _balancesRepository = balancesRepository;
            _itemValidation = itemValidation;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(UpdateStockAdjustmentDraftCommand request,CancellationToken cancellationToken)
        {
            // Phase 1: Validate the tenant and load its adjustment.
            var tenantId = _currentUser.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            var adjustment = await _adjustmentsRepository.GetForUpdateAsync(
                tenantId.Value,
                request.AdjustmentId,
                cancellationToken);

            if (adjustment == null)
                return Result<Guid>.Failure(
                    "Stock adjustment was not found.");

            // Phase 2: Ensure the adjustment is still an unchanged draft.
            if (adjustment.Status != StockAdjustmentStatus.Draft)
                return Result<Guid>.Failure(
                    "Only draft adjustments can be edited.");

            if (!adjustment.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This adjustment has changed. Reload it before editing.");

            // Phase 3: Find existing items and identify omitted items.
            var existingItems = adjustment.Items.ToDictionary(
                item => (item.ProductId, item.ProductVariantId));

            var requestedKeys = request.Items
                .Select(item => (item.ProductId, item.ProductVariantId))
                .ToHashSet();

            var removedItems = adjustment.Items
                .Where(item => !requestedKeys.Contains(
                    (item.ProductId, item.ProductVariantId)))
                .ToList();

            // Phase 4: Update counted quantities or add new items.
            foreach (var input in request.Items)
            {
                var key = (input.ProductId, input.ProductVariantId);
                existingItems.TryGetValue(key, out var item);

                if (item != null && item.NewQuantity == input.NewQuantity)
                    continue;

                await _itemValidation.ValidateStockItemAsync(
                    tenantId.Value,
                    adjustment.BranchId,
                    input.ProductId,
                    input.ProductVariantId,
                    input.NewQuantity,
                    cancellationToken);

                // Only new items capture the balance quantity and version.
                if (item == null)
                {
                    var balance =
                        await _balancesRepository.GetProductStockBalanceAsync(
                            tenantId: tenantId.Value,
                            branchId: adjustment.BranchId,
                            productId: input.ProductId,
                            productVariantId: input.ProductVariantId,
                            cancellationToken: cancellationToken);

                    if (balance == null)
                        return Result<Guid>.Failure(
                            $"No stock balance exists for product " +
                            $"{input.ProductId} and variant {input.ProductVariantId}.");

                    item = new StockAdjustmentItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId.Value,
                        AdjustmentId = adjustment.Id,
                        ProductId = input.ProductId,
                        ProductVariantId = input.ProductVariantId,
                        OldQuantity = balance.QuantityOnHand,
                        BalanceRowVersionAtCount = balance.RowVersion.ToArray()
                    };

                    adjustment.Items.Add(item);
                }

                // Existing items retain their original quantity and balance version.
                var delta = input.NewQuantity - item.OldQuantity;

                const decimal maxQuantity = 999999999999999.999m;

                if (delta < -maxQuantity || delta > maxQuantity)
                    return Result<Guid>.Failure(
                        "The quantity difference exceeds the supported range.");

                item.NewQuantity = input.NewQuantity;
                item.QuantityDelta = delta;
            }

            // Phase 5: Remove items omitted from the submitted list.
            _adjustmentsRepository.RemoveItems(removedItems);

            foreach (var item in removedItems)
                adjustment.Items.Remove(item);

            // Phase 6: Update the reason and modification time.
            // Updating the adjustment also triggers its row-version check.
            adjustment.Reason = request.Reason.Trim();
            adjustment.UpdatedAt = DateTime.UtcNow;

            // Phase 7: Save all draft changes together.
            // Stock balances and stock movements remain unchanged.
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<Guid>.Failure(
                    "This adjustment changed while you were editing it. " +
                    "Reload it and try again.");
            }

            return Result<Guid>.Success(adjustment.Id);
        }
    }
}
