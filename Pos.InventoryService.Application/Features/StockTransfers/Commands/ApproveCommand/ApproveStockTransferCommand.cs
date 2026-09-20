using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockTransfers.DTOs;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.ApproveCommand
{
    public class ApproveStockTransferCommand : IRequest<Result<Guid>>
    {
        public Guid TransferId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class ApproveStockTransferCommandHandler : IRequestHandler<ApproveStockTransferCommand, Result<Guid>>
    {
        private readonly IStockTransferRepositoryAsync _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public ApproveStockTransferCommandHandler(
            IStockTransferRepositoryAsync repository,
            ICurrentUserService currentUser,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            ApproveStockTransferCommand request,
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

            if (transfer.Status != StockTransferStatus.Requested)
                return Result<Guid>.Failure("Only requested transfers can be approved.");

            if (!transfer.RowVersion.SequenceEqual(request.RowVersion))
                return Result<Guid>.Failure(
                    "This transfer has changed. Reload it and try again.");

            // 3. Approve the requested transfer without moving stock.
            if (transfer.Items == null || transfer.Items.Count == 0)
                return Result<Guid>.Failure("The transfer must contain at least one item.");

            transfer.Status = StockTransferStatus.Approved;
            transfer.ApprovedByUserId = userId;

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

