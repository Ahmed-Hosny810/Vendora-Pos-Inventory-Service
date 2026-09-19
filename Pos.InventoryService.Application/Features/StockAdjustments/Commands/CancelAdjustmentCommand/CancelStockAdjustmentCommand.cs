using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;


namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.CancelAdjustmentCommand
{
    public class CancelStockAdjustmentCommand : IRequest<Result>
    {
        public Guid AdjustmentId { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public class CancelStockAdjustmentCommandHandler: IRequestHandler<CancelStockAdjustmentCommand, Result>
    {
        private readonly IStockAdjustmentRepository _stockAdjustmentRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CancelStockAdjustmentCommandHandler(
            IStockAdjustmentRepository stockAdjustmentRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _stockAdjustmentRepository = stockAdjustmentRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(
            CancelStockAdjustmentCommand request,
            CancellationToken cancellationToken)
        {
            // 1. Get the authenticated tenant.
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            // 2. Load the tracked adjustment belonging to this tenant.
            var adjustment =
                await _stockAdjustmentRepository.GetForUpdateAsync(
                    tenantId.Value,
                    request.AdjustmentId,
                    cancellationToken);

            if (adjustment == null)
                return Result.Failure(
                    "Stock adjustment was not found.");

            // 3. Treat a repeated cancellation as successful.
            if (adjustment.Status == StockAdjustmentStatus.Cancelled)
                return Result.Success();

            // 4. Only draft or approved adjustments can be cancelled.
            if (adjustment.Status != StockAdjustmentStatus.Draft &&
                adjustment.Status != StockAdjustmentStatus.Approved)
            {
                return Result.Failure(
                    "Only draft or approved adjustments can be cancelled.");
            }

            // 5. Ensure the user is cancelling the version they reviewed.
            if (!adjustment.RowVersion.SequenceEqual(request.RowVersion))
            {
                return Result.Failure(
                    "This adjustment has changed. Reload it before cancelling.");
            }

            adjustment.Status = StockAdjustmentStatus.Cancelled;
            adjustment.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure(
                    "This adjustment changed while you were cancelling it. " +
                    "Reload it and try again.");
            }

            return Result.Success();
        }
    }
}
