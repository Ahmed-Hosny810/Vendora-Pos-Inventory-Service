using MediatR;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;

public class RestockCustomerReturnCommandHandler : IRequestHandler<RestockCustomerReturnCommand, Result<Guid>>
{
    private readonly IStockBalanceRepositoryAsync _balances;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryItemValidationService _validation;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RestockCustomerReturnCommandHandler(
        IStockBalanceRepositoryAsync balances,
        IStockMovementRepository movements,
        IInventoryItemValidationService validation,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _balances = balances;
        _movements = movements;
        _validation = validation;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        RestockCustomerReturnCommand request, CancellationToken cancellationToken)
    {
        // 1. Get the authenticated tenant and user.
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant is required.");

        if (!Guid.TryParse(_currentUser.UserId, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid user is required.");

        // 2. A completed return must not increase stock again.
        if (await _movements.ReturnRestockExistsAsync(
            tenantId.Value, request.ReturnId, cancellationToken))
        {
            return Result<Guid>.Success(request.ReturnId);
        }

        // 3. Validate all products, including goods that will not be restocked.
        foreach (var item in request.Items)
        {
            await _validation.ValidateStockItemAsync(
                tenantId.Value, request.BranchId,
                item.ProductId, item.ProductVariantId, item.Quantity,
                cancellationToken);
        }

        // 4. Combine sellable quantities of the same product/variant into one movement.
        // Non-restockable goods create neither balance changes nor movements.
        var restockItems = request.Items.Where(x => x.Restock)
            .GroupBy(x => (x.ProductId, x.ProductVariantId));
        var now = DateTime.UtcNow;
        const decimal maxQuantity = 999999999999999.999m;

        foreach (var items in restockItems)
        {
            var quantity = items.Sum(x => x.Quantity);
            if (quantity > maxQuantity)
                return Result<Guid>.Failure("The combined return quantity exceeds the supported range.");

            // 5. Load the tracked destination balance, or create it if missing.
            var balance = await _balances.GetProductStockBalanceAsync(
                tenantId.Value, request.BranchId,
                items.Key.ProductId, items.Key.ProductVariantId,
                cancellationToken);

            if (balance == null)
            {
                balance = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    BranchId = request.BranchId,
                    ProductId = items.Key.ProductId,
                    ProductVariantId = items.Key.ProductVariantId,
                    QuantityOnHand = 0,
                    QuantityReserved = 0,
                    LowStockThreshold = 0,
                    CreatedAt = now
                };
                await _balances.AddAsync(balance, cancellationToken);
            }

            if (balance.QuantityOnHand > maxQuantity - quantity)
                return Result<Guid>.Failure("The resulting stock quantity exceeds the supported range.");

            // 6. Increase on-hand stock; reserved quantities remain unchanged.
            var beforeQuantity = balance.QuantityOnHand;
            balance.QuantityOnHand += quantity;
            balance.UpdatedAt = now;

            await _movements.AddAsync(new StockMovement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                BranchId = request.BranchId,
                ProductId = items.Key.ProductId,
                ProductVariantId = items.Key.ProductVariantId,
                MovementType = StockMovementType.Return,
                ReferenceType = StockReferenceType.Return,
                ReferenceId = request.ReturnId,
                IdempotencyKey = request.IdempotencyKey,
                BeforeQty = beforeQuantity,
                AfterQty = balance.QuantityOnHand,
                QuantityDelta = quantity,
                LowStockThreshold = balance.LowStockThreshold,
                CreatedByUserId = userId,
                CreatedAt = now
            }, cancellationToken);
        }

        // 7. Save balances and movements together.
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<Guid>.Failure(
                "Stock changed while restocking. Retry the same return with the same idempotency key.");
        }
        catch (DuplicateStockWriteException)
        {
            // A concurrent copy may already have completed this return.
            if (await _movements.ReturnRestockExistsAsync(
                tenantId.Value, request.ReturnId, cancellationToken))
            {
                return Result<Guid>.Success(request.ReturnId);
            }

            return Result<Guid>.Failure(
                "A stock write conflicted. Retry the same return with the same idempotency key.");
        }

        return Result<Guid>.Success(request.ReturnId);
    }
}

