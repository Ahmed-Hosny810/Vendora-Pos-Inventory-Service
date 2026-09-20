using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockTransfers.DTOs;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.RequestCommand
{
    public class RequestStockTransferCommand : IRequest<Result<Guid>>
    {
        public Guid TransferId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class RequestStockTransferCommandHandler : IRequestHandler<RequestStockTransferCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventoryItemValidationService _itemValidationService;

        public RequestStockTransferCommandHandler(
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
            RequestStockTransferCommand request,
            CancellationToken cancellationToken)
        {
            // 1. Require the authenticated tenant.
            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException("A valid tenant is required.");

            if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId == Guid.Empty)
                throw new UnauthorizedAccessException("A valid user is required.");

            // 2. Load the tracked transfer and verify its status and version.
            var transfer = await _repository.GetByIdAsync(
                tenantId.Value, request.TransferId, cancellationToken);

            if (transfer == null)
                return Result<Guid>.Failure("Stock transfer was not found.");

            if (transfer.Status != StockTransferStatus.Draft)
                return Result<Guid>.Failure("Only draft transfers can be submitted.");

            if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This transfer has changed. Reload it and try again.");

            // 3. Revalidate the saved draft before submitting it.

            if (transfer.Items == null || transfer.Items.Count == 0)
                return Result<Guid>.Failure("The transfer must contain at least one item.");

            if (transfer.FromBranchId == transfer.ToBranchId)
                return Result<Guid>.Failure("Source and destination branches must be different.");

            if (transfer.Items.Any(item => item.Quantity <= 0 || item.ReceivedQuantity != 0))
                return Result<Guid>.Failure("The draft contains invalid item quantities.");

            foreach (var item in transfer.Items)
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

            transfer.Status = StockTransferStatus.Requested;
            transfer.RequestedByUserId = userId;


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

