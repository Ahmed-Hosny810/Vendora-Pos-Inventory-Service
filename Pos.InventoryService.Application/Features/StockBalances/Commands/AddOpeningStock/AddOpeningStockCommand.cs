
using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockBalances.Commands.AddOpeningStock
{
    public class AddOpeningStockCommand:IRequest<Result<Guid>>
    {
        public Guid RequestId { get; set; }
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal LowStockThreshold { get; set; }
    }
    public class AddOpeningStockCommandHandler
        : IRequestHandler<AddOpeningStockCommand, Result<Guid>>
    {
        private readonly IInventoryItemValidationService _itemValidationService;
        private readonly IStockBalanceRepositoryAsync _stockBalanceRepository;
        private readonly IStockMovementRepository _stockMovementRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public AddOpeningStockCommandHandler(
            IInventoryItemValidationService itemValidationService,
            IStockBalanceRepositoryAsync stockBalanceRepository,
            IStockMovementRepository stockMovementRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _itemValidationService = itemValidationService;
            _stockBalanceRepository = stockBalanceRepository;
            _stockMovementRepository = stockMovementRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(
            AddOpeningStockCommand request,
            CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                return Result<Guid>.Failure(
                    "TenantId claim is missing or invalid.");
            }

            if (!Guid.TryParse(_currentUserService.UserId, out var userId)
                || userId == Guid.Empty)
            {
                return Result<Guid>.Failure(
                    "UserId claim is missing or invalid.");
            }

            var existingMovement =
                await _stockMovementRepository.GetOpeningMovementByRequestIdAsync(tenantId.Value,request.RequestId,cancellationToken);

            if (existingMovement != null)
                return GetReplayResult(existingMovement, request);

            await _itemValidationService.ValidateOpeningStockAsync(
                tenantId.Value,
                request.BranchId,
                request.ProductId,
                request.ProductVariantId,
                request.Quantity,
                cancellationToken);

            var existingBalance =
                await _stockBalanceRepository.GetProductStockBalanceAsync(
                    tenantId: tenantId.Value,
                    branchId: request.BranchId,
                    productId: request.ProductId,
                    productVariantId: request.ProductVariantId,
                    cancellationToken: cancellationToken);

            if (existingBalance != null)
            {
                existingMovement =
                    await _stockMovementRepository.GetOpeningMovementByRequestIdAsync(tenantId.Value,request.RequestId,cancellationToken);

                if (existingMovement != null)
                    return GetReplayResult(existingMovement, request);

                return Result<Guid>.Failure("Stock is already initialized for this item.");
            }

            var now = DateTime.UtcNow;

            var balance = new StockBalance
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                BranchId = request.BranchId,
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                QuantityOnHand = request.Quantity,
                QuantityReserved = 0,
                LowStockThreshold = request.LowStockThreshold,
                CreatedAt = now
            };

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                BranchId = request.BranchId,
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,

                MovementType = StockMovementType.OpeningStock,
                ReferenceType = StockReferenceType.OpeningStock,
                ReferenceId = request.RequestId,

                QuantityDelta = request.Quantity,
                BeforeQty = 0,
                AfterQty = request.Quantity,
                LowStockThreshold = request.LowStockThreshold,

                CreatedByUserId = userId,
                CreatedAt = now
            };

            await _stockBalanceRepository.AddAsync(
                balance, cancellationToken);

            await _stockMovementRepository.AddAsync(
                movement, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DuplicateStockWriteException)
            {
                existingMovement =
                    await _stockMovementRepository
                        .GetOpeningMovementByRequestIdAsync(
                            tenantId.Value,
                            request.RequestId,
                            cancellationToken);

                if (existingMovement != null)
                    return GetReplayResult(existingMovement, request);

                existingBalance =
                    await _stockBalanceRepository.GetProductStockBalanceAsync(
                        tenantId: tenantId.Value,
                        branchId: request.BranchId,
                        productId: request.ProductId,
                        productVariantId: request.ProductVariantId,
                        cancellationToken: cancellationToken);

                if (existingBalance != null)
                {
                    return Result<Guid>.Failure(
                        "Stock is already initialized for this item.");
                }

                throw;
            }

            return Result<Guid>.Success(movement.Id);
        }

        private static Result<Guid> GetReplayResult(
            StockMovement movement,
            AddOpeningStockCommand request)
        {
            var sameRequest =
                movement.BranchId == request.BranchId &&
                movement.ProductId == request.ProductId &&
                movement.ProductVariantId == request.ProductVariantId &&
                movement.QuantityDelta == request.Quantity &&
                movement.LowStockThreshold == request.LowStockThreshold;

            if (!sameRequest)
            {
                return Result<Guid>.Failure(
                    "RequestId was already used with different inputs.");
            }

            return Result<Guid>.Success(movement.Id);
        }
    }
}
