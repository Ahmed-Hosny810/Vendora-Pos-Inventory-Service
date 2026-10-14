using MediatR;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Common.Options;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockBalances.DTOs;
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
                throw new UnauthorizedAccessException("A valid tenant is required.");

            // 2. Check whether this sale already has a reservation.
            var existingReservation = await _stockReservationRepository.GetByReferenceIdAsync(
                tenantId.Value, request.SaleId, cancellationToken);

            if (existingReservation != null)
                return ValidateExistingReservation(existingReservation, request);

            // 3. Validate every requested item in a single batch.
            var validationResult = await _itemValidationService.ValidateStockItemsAsync(
                tenantId.Value, request.BranchId, request.Items, cancellationToken);

            if (validationResult.IsFailure)
                return Result<Guid>.Failure(validationResult.Errors.ToArray());

            // 4. Load every needed stock balance in a single batch, not one call per item.
            var balances = await _stockBalanceRepository.GetBatchStockAvailabilityAsync(
                tenantId: tenantId.Value,
                branchId: request.BranchId,
                items: request.Items.Select(i => new StockAvailabilityItem { ProductId=i.ProductId,ProductVariantId=i.ProductVariantId} ).ToList(),
                cancellationToken: cancellationToken);

            var balanceByKey = balances.ToDictionary(b => (b.ProductId, b.ProductVariantId));

            // 5. Create the reservation and calculate its expiry.
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
                if (!balanceByKey.TryGetValue((item.ProductId, item.ProductVariantId), out var balance))
                    return Result<Guid>.Failure($"Stock balance for product {item.ProductId} was not found.");

                if (item.Quantity > balance.AvailableQuantity)
                    return Result<Guid>.Failure($"Insufficient stock for product {item.ProductId}.");

                reservation.Items.Add(new StockReservationItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    ReservationId = reservation.Id,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,
                    Quantity = item.Quantity
                });

                // Increase reserved stock without changing on-hand stock.
                balance.QuantityReserved += item.Quantity;
                balance.UpdatedAt = now;
            }

            // 6. Save the reservation, items and balance changes together.
            await _stockReservationRepository.AddAsync(reservation, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                return Result<Guid>.Failure(
                    "Stock changed while reserving it. Reload and try again.");
            }

            return Result<Guid>.Success(reservation.Id);
        }

        private static Result<Guid> ValidateExistingReservation(
            StockReservation existingReservation, CreateStockReservationCommand request)
        {
            if (existingReservation.Status != StockReservationStatus.Active)
                return Result<Guid>.Failure($"This reservation is already {existingReservation.Status}.");

            if (existingReservation.ExpiresAt <= DateTime.UtcNow)
                return Result<Guid>.Failure("This reservation has expired.");

            if (existingReservation.BranchId != request.BranchId)
                return Result<Guid>.Failure("The existing reservation belongs to a different branch.");

            // The cart may have changed between attempts 
            if (!ItemsMatch(existingReservation.Items, request.Items))
                return Result<Guid>.Failure(
                    "The sale's items changed since the reservation was created. " +
                    "Release the existing reservation before retrying.");

            return Result<Guid>.Success(existingReservation.Id);
        }

        private static bool ItemsMatch(
            ICollection<StockReservationItem> existingItems,
            List<StockReservationItemDto> requestedItems)
        {
            if (existingItems.Count != requestedItems.Count)
                return false;

            var existingByKey = existingItems.ToDictionary(i => (i.ProductId, i.ProductVariantId), i => i.Quantity);

            foreach (var item in requestedItems)
            {
                if (!existingByKey.TryGetValue((item.ProductId, item.ProductVariantId), out var existingQuantity))
                    return false;

                if (existingQuantity != item.Quantity)
                    return false;
            }

            return true;
        }
    }
}