using MediatR;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Common.Options;
using Pos.InventoryService.Application.Features.StockReservations.DTOS;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockReservations.Commands.CreateCommand
{
    public class CreateStockReservationCommand : IRequest<Result<Guid>>
    {
        public Guid SaleId { get; set; }
        public Guid BranchId { get; set; }

        public List<StockReservationItemDto> Items { get; set; } = new();
    }

    public class CreateStockReservationCommandHandler
        : IRequestHandler<CreateStockReservationCommand, Result<Guid>>
    {
        private readonly IStockReservationRepositoryAsync _stockReservationRepository;
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly IInventoryItemValidationService _itemValidationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly StockReservationOptions _reservationOptions;

        public CreateStockReservationCommandHandler(
            IStockReservationRepositoryAsync stockReservationRepository,
            IStockBalanceRepositoryAsync stockBalanceRepository,
            IInventoryItemValidationService itemValidationService,
            ICurrentUserService currentUserService,
            IOptions<StockReservationOptions> reservationOptions,
            IUnitOfWork unitOfWork)
        {
            _stockReservationRepository = stockReservationRepository;
            _stockBalanceRepository = stockBalanceRepository;
            _itemValidationService = itemValidationService;
            _currentUserService = currentUserService;
            _reservationOptions = reservationOptions.Value;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            CreateStockReservationCommand request,
            CancellationToken cancellationToken)
        {
            // 1. Get the authenticated tenant.
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            // 2. Check whether this sale already has a reservation.
            var existingReservation =
                await _stockReservationRepository.GetByIdAsync(
                    tenantId.Value,
                    request.SaleId,
                    cancellationToken);

            if (existingReservation != null)
            {
                if (existingReservation.Status != StockReservationStatus.Active)
                {
                    return Result<Guid>.Failure(
                        $"This reservation is already {existingReservation.Status}.");
                }

                if (existingReservation.ExpiresAt <= DateTime.UtcNow)
                    return Result<Guid>.Failure(
                        "This reservation has expired.");

                return Result<Guid>.Success(existingReservation.Id);
            }

            // 3. Create the reservation and calculate its expiry.
            var now = DateTime.UtcNow;

            var reservation = new StockReservation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                BranchId = request.BranchId,
                ReferenceId = request.SaleId,
                ReferenceType = StockReferenceType.Sale,
                Status = StockReservationStatus.Active,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_reservationOptions.ExpiryMinutes)
            };

            foreach (var item in request.Items)
            {
                // 4. Validate the branch, product, variant and unit rules.
                await _itemValidationService.ValidateStockItemAsync(
                    tenantId.Value,
                    request.BranchId,
                    item.ProductId,
                    item.ProductVariantId,
                    item.Quantity,
                    cancellationToken);

                // 5. Load the tracked balance and check available stock.
                var balance =
                    await _stockBalanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: request.BranchId,
                        productId: item.ProductId,
                        productVariantId: item.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (balance == null)
                {
                    return Result<Guid>.Failure(
                        $"Stock balance for product {item.ProductId} was not found.");
                }

                if (item.Quantity > balance.AvailableQuantity)
                {
                    return Result<Guid>.Failure(
                        $"Insufficient stock for product {item.ProductId}.");
                }

                // 6. Add the reservation item.
                reservation.Items.Add(new StockReservationItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    ReservationId = reservation.Id,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity
                });

                // 7. Increase reserved stock without changing on-hand stock.
                balance.QuantityReserved += item.Quantity;
                balance.UpdatedAt = now;
            }

            // 8. Save the reservation, items and balance changes together.
            await _stockReservationRepository.AddAsync(
                reservation,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(reservation.Id);
        }
    }
}