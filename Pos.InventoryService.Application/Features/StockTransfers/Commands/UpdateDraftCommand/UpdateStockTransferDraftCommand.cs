using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockTransfers.DTOs;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.UpdateDraftCommand
{
    public class UpdateStockTransferDraftCommand : IRequest<Result<Guid>>
    {
        public Guid TransferId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public List<StockTransferItemDto> Items { get; set; } = new();
    }

    public class UpdateStockTransferDraftCommandHandler : IRequestHandler<UpdateStockTransferDraftCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventoryItemValidationService _itemValidationService;

        public UpdateStockTransferDraftCommandHandler(
            IStockTransferRepositoryAsync repository,
            ICurrentUserService currentUser,
            IInventoryItemValidationService itemValidationService,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _itemValidationService = itemValidationService;
        }

        public async Task<Result<Guid>> Handle(
            UpdateStockTransferDraftCommand request,
            CancellationToken cancellationToken)
        {
            // 1. Require the authenticated tenant.
            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException("A valid tenant is required.");

            // 2. Load the tracked transfer and verify its status and version.
            var transfer = await _repository.GetByIdAsync(
                tenantId.Value, request.TransferId, cancellationToken);

            if (transfer == null)
                return Result<Guid>.Failure("Stock transfer was not found.");

            if (transfer.Status != StockTransferStatus.Draft)
                return Result<Guid>.Failure("Only draft transfers can be edited.");

            if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This transfer has changed. Reload it and try again.");

            // 3. Validate every requested item before editing the tracked transfer.
            foreach (var item in request.Items)
            {
                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value, transfer.FromBranchId,
                    item.ProductId, item.ProductVariantId, item.Quantity,
                    cancellationToken);
                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value, transfer.ToBranchId,
                    item.ProductId, item.ProductVariantId, item.Quantity,
                    cancellationToken);
            }

            // 4. Remove omitted items; preserve IDs for existing items.
            var requestedKeys = request.Items
                .Select(item => (item.ProductId, item.ProductVariantId))
                .ToHashSet();
            var removedItems = transfer.Items
                .Where(item => !requestedKeys.Contains((item.ProductId, item.ProductVariantId)))
                .ToList();

            var existingItems = transfer.Items.ToDictionary(
                item => (item.ProductId, item.ProductVariantId));

            _repository.RemoveItems(removedItems);
            foreach (var item in removedItems)
                transfer.Items.Remove(item);

            // 5. Update quantities or add new items. Branches remain fixed.
            foreach (var input in request.Items)
            {
                if (existingItems.TryGetValue(
                    (input.ProductId, input.ProductVariantId), out var item))
                {
                    item.Quantity = input.Quantity;
                }
                else
                {
                    transfer.Items.Add(new StockTransferItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId.Value,
                        TransferId = transfer.Id,
                        ProductId = input.ProductId,
                        ProductVariantId = input.ProductVariantId,
                        Quantity = input.Quantity,
                        ReceivedQuantity = 0
                    });
                }
            }

            transfer.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<Guid>.Failure(
                    "This transfer changed while saving. Reload it and try again.");
            }

            return Result<Guid>.Success(transfer.Id);
        }
    }
}

