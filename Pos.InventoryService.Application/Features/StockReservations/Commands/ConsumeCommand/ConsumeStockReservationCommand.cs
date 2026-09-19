using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;


namespace Pos.InventoryService.Application.Features.StockReservations.Commands.ConsumeCommand
{
    public class ConsumeStockReservationCommand : IRequest<Result<Guid>>
    {
        public Guid ReservationId { get; set; }
    }

    public class ConsumeStockReservationCommandHandler
        : IRequestHandler<ConsumeStockReservationCommand, Result<Guid>>
    {
        private readonly IStockReservationRepositoryAsync _stockReservationRepository;
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly IStockMovementRepository _stockMovementRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public ConsumeStockReservationCommandHandler(
            IStockReservationRepositoryAsync stockReservationRepository,
            IStockBalanceRepositoryAsync stockBalanceRepository,
            IStockMovementRepository stockMovementRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _stockReservationRepository = stockReservationRepository;
            _stockBalanceRepository = stockBalanceRepository;
            _stockMovementRepository = stockMovementRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            ConsumeStockReservationCommand request,
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

            // 2. Load the tracked reservation with its items.
            var reservation =
                await _stockReservationRepository.GetByIdAsync(
                    tenantId.Value,
                    request.ReservationId,
                    cancellationToken);

            if (reservation == null)
                return Result<Guid>.Failure(
                    "Stock reservation was not found.");

            // 3. Return success for a previously completed reservation.
            if (reservation.Status == StockReservationStatus.Consumed)
                return Result<Guid>.Success(reservation.Id);

            // 4. Only active, unexpired reservations can be consumed.
            if (reservation.Status != StockReservationStatus.Active)
            {
                return Result<Guid>.Failure(
                    $"A reservation with status {reservation.Status} cannot be consumed.");
            }

            var now = DateTime.UtcNow;

            if (reservation.ExpiresAt <= now)
                return Result<Guid>.Failure(
                    "This reservation has expired.");

            if (reservation.Items == null || reservation.Items.Count == 0)
                return Result<Guid>.Failure(
                    "The reservation has no items.");

            foreach (var item in reservation.Items)
            {
                // 5. Load the tracked balance for this reservation item.
                var balance =
                    await _stockBalanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: reservation.BranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                {
                    return Result<Guid>.Failure(
                        $"Stock balance for product {item.ProductId} was not found.");
                }

                // 6. Check that the reserved and on-hand quantities cover the item.
                if (balance.QuantityReserved < item.Quantity)
                {
                    return Result<Guid>.Failure(
                        $"Reserved stock for product {item.ProductId} " +
                        "is less than the reservation quantity.");
                }

                if (balance.QuantityOnHand < item.Quantity)
                {
                    return Result<Guid>.Failure(
                        $"Insufficient  stock for product {item.ProductId}.");
                }

                // 7. Deduct the sold quantity from on-hand and reserved stock.
                var beforeQuantity = balance.QuantityOnHand;

                balance.QuantityOnHand -= item.Quantity;
                balance.QuantityReserved -= item.Quantity;
                balance.UpdatedAt = now;

                // 8. Record the sale movement.
                var movement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    BranchId = reservation.BranchId,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,

                    MovementType = StockMovementType.Sale,
                    BeforeQty = beforeQuantity,
                    AfterQty = balance.QuantityOnHand,
                    QuantityDelta = -item.Quantity,

                    ReferenceType = StockReferenceType.Sale,
                    ReferenceId = reservation.ReferenceId,

                    LowStockThreshold = balance.LowStockThreshold,
                    CreatedByUserId = userId,
                    CreatedAt = now
                };

                await _stockMovementRepository.AddAsync(
                    movement,
                    cancellationToken);
            }

            // 9. Mark the reservation as consumed.
            reservation.Status = StockReservationStatus.Consumed;
            reservation.UpdatedAt = now;

            // 10. Save all changes together.
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<Guid>.Failure(
                    "The reservation or its stock balances changed during " +
                    "completion. Retry the request.");
            }

            return Result<Guid>.Success(reservation.Id);
        }
    }
}
