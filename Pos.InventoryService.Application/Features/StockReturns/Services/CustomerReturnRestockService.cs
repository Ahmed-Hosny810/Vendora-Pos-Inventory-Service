using Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Features.StockReturns.Services;

public class CustomerReturnRestockService : ICustomerReturnRestockService
{
    private readonly IStockBalanceRepositoryAsync _balances;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryItemValidationService _validation;

    public CustomerReturnRestockService(
        IStockBalanceRepositoryAsync balances,
        IStockMovementRepository movements,
        IInventoryItemValidationService validation)
    {
        _balances = balances;
        _movements = movements;
        _validation = validation;
    }

    public async Task<Result<Guid>> StageRestockAsync(
        Guid tenantId,
        Guid userId,
        Guid returnId,
        Guid branchId,
        Guid idempotencyKey,
        IReadOnlyList<RestockCustomerReturnItemDto> itemsToReturn,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            return Result<Guid>.Failure("A valid tenant and actor are required.");
        // 2. A completed return must not increase stock again.
        if (await _movements.ReturnRestockExistsAsync(
            tenantId, returnId, cancellationToken))
        {
            return Result<Guid>.Success(returnId);
        }

        // 3. Validate all products, including goods that will not be restocked.
        foreach (var item in itemsToReturn)
        {
            await _validation.ValidateStockItemAsync(
                tenantId, branchId,
                item.ProductId, item.ProductVariantId, item.Quantity,
                cancellationToken);
        }

        // 4. Combine sellable quantities of the same product/variant into one movement.
        // Non-restockable goods create neither balance changes nor movements.
        var restockItems = itemsToReturn.Where(x => x.Restock)
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
                tenantId, branchId,
                items.Key.ProductId, items.Key.ProductVariantId,
                cancellationToken);

            if (balance == null)
            {
                balance = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BranchId = branchId,
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
                TenantId = tenantId,
                BranchId = branchId,
                ProductId = items.Key.ProductId,
                ProductVariantId = items.Key.ProductVariantId,
                MovementType = StockMovementType.Return,
                ReferenceType = StockReferenceType.Return,
                ReferenceId = returnId,
                IdempotencyKey = idempotencyKey,
                BeforeQty = beforeQuantity,
                AfterQty = balance.QuantityOnHand,
                QuantityDelta = quantity,
                LowStockThreshold = balance.LowStockThreshold,
                CreatedByUserId = userId,
                CreatedAt = now
            }, cancellationToken);
        }

        // The caller commits these changes
        return Result<Guid>.Success(returnId);
    }
}