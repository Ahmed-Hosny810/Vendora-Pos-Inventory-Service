using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Infrastructure.Shared.Services
{
    public class StockReservationReleaseService : IStockReservationReleaseService
    {
        private readonly IStockReservationRepositoryAsync _reservationRepository;
        private readonly IStockBalanceRepositoryAsync _balanceRepository;
        private readonly IUnitOfWork _unitOfWork;

        public StockReservationReleaseService(
            IStockReservationRepositoryAsync reservationRepository,
            IStockBalanceRepositoryAsync balanceRepository,
            IUnitOfWork unitOfWork)
        {
            _reservationRepository = reservationRepository;
            _balanceRepository = balanceRepository;
            _unitOfWork = unitOfWork;
        }

        public Task<Result> ReleaseAsync(
            Guid tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            return ReleaseReservationAsync(
                tenantId,
                reservationId,
                false,
                cancellationToken);
        }

        public Task<Result> ExpireAsync(
            Guid tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            return ReleaseReservationAsync(
                tenantId,
                reservationId,
                true,
                cancellationToken);
        }

        private async Task<Result> ReleaseReservationAsync(
            Guid tenantId,
            Guid reservationId,
            bool expire,
            CancellationToken cancellationToken)
        {
            // 1. Require a valid tenant for every lookup.
            if (tenantId == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            // 2. Load the tracked reservation with its items.
            var reservation = await _reservationRepository.GetByIdAsync(
                tenantId,
                reservationId,
                cancellationToken);

            if (reservation == null)
                return Result.Failure("Stock reservation was not found.");

            var now = DateTime.UtcNow;

            // 3. rules for expiration or explicit release.
            if (expire)
            {
                // The worker skips reservations that no longer need expiration.
                if (reservation.Status != StockReservationStatus.Active ||
                    reservation.ExpiresAt > now)
                {
                    return Result.Success();
                }
            }
            else
            {
                // Repeated release must not subtract reserved stock again.
                if (reservation.Status == StockReservationStatus.Released ||
                    reservation.Status == StockReservationStatus.Expired)
                {
                    return Result.Success();
                }

                if (reservation.Status != StockReservationStatus.Active)
                {
                    return Result.Failure(
                        $"A reservation with status {reservation.Status} " +
                        "cannot be released.");
                }
            }

            // 4. Ensure the active reservation contains items.
            if (reservation.Items == null || reservation.Items.Count == 0)
                return Result.Failure("The reservation has no items.");

            foreach (var item in reservation.Items)
            {
                // 5. Reject invalid saved quantities.
                if (item.Quantity <= 0)
                {
                    return Result.Failure(
                        $"The reservation quantity for product " +
                        $"{item.ProductId} is invalid.");
                }

                // 6. Load the item's tracked stock balance.
                var balance =
                    await _balanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId,
                        branchId: reservation.BranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                {
                    return Result.Failure(
                        $"Stock balance for product {item.ProductId} was not found.");
                }

                // 7. Ensure reserved stock covers the quantity being released.
                if (balance.QuantityReserved < item.Quantity)
                {
                    return Result.Failure(
                        $"Reserved stock for product {item.ProductId} " +
                        "is less than the reservation quantity.");
                }

                // 8. Release reserved stock without changing on-hand stock.
                balance.QuantityReserved -= item.Quantity;
                balance.UpdatedAt = now;
            }

            // 9. Record why the reservation ended.
            reservation.Status = expire? StockReservationStatus.Expired : StockReservationStatus.Released;

            reservation.UpdatedAt = now;

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Failure(
                    "The reservation or its stock balances changed. " +
                    "Retry the operation with fresh data.");
            }

            return Result.Success();
        }
    }

}
