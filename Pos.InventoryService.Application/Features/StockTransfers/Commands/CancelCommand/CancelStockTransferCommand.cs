using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockTransfers.DTOs;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.CancelCommand
{
    public class CancelStockTransferCommand : IRequest<Result<Guid>>
    {
        public Guid TransferId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class CancelStockTransferCommandHandler : IRequestHandler<CancelStockTransferCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public CancelStockTransferCommandHandler(
            IStockTransferRepositoryAsync repository,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            CancelStockTransferCommand request,
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

            if (transfer.Status == StockTransferStatus.Cancelled)
                return Result<Guid>.Success(transfer.Id);

            if (transfer.Status != StockTransferStatus.Draft &&
                transfer.Status != StockTransferStatus.Requested &&
                transfer.Status != StockTransferStatus.Approved)
            {
                return Result<Guid>.Failure(
                    "Only pre-dispatch transfers can be cancelled. " +
                    "Dispatched goods require explicit resolution.");
            }

            if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This transfer has changed. Reload it and try again.");

            // 3. Cancel before dispatch; there are no stock changes to undo.
            transfer.Status = StockTransferStatus.Cancelled;

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

