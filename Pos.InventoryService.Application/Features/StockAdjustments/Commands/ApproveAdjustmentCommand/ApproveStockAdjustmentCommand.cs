using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.ApproveAdjustmentCommand
{
    public class ApproveStockAdjustmentCommand:IRequest<Result>
    {
        public Guid AdjustmentId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class ApproveStockAdjustmentCommandHandler : IRequestHandler<ApproveStockAdjustmentCommand, Result>
    {
        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public ApproveStockAdjustmentCommandHandler(
            IStockAdjustmentRepository stockAdjustmentRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(ApproveStockAdjustmentCommand request, CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            if (!Guid.TryParse(_currentUserService.UserId,out var userId) || userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException(
                    "A valid user is required.");
            }

            var adjustment= await _stockAdjustmentRepository.GetForUpdateAsync(tenantId.Value,request.AdjustmentId,cancellationToken);

            if (adjustment == null)
                return Result.Failure(
                    "Stock adjustment was not found.");

            //  Ensure the adjustment is still an unchanged draft.
            if (adjustment.Status != StockAdjustmentStatus.Draft)
                return Result.Failure(
                    "Only draft adjustments can be approved.");

            if (!adjustment.RowVersion.SequenceEqual(request.RowVersion))
                return Result.Failure(
                    "This adjustment has changed. Reload it before approving.");

            if (adjustment.Items == null || adjustment.Items.Count == 0)
                return Result.Failure("An adjustment must contain at least one item.");

            adjustment.Status = StockAdjustmentStatus.Approved;
            adjustment.ApprovedByUserId = userId;
            adjustment.UpdatedAt= DateTime.UtcNow;

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure(
                    "This adjustment changed while you were approving it. " +
                    "Reload it and try again.");
            }

            return Result.Success();
        }
    }
}
