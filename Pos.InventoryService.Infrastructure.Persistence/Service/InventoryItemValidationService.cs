using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockReservations.DTOS;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.ReadModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pos.InventoryService.Infrastructure.Persistence.Service
{
    /// <summary>
    /// Implements inventory item validation through EF Core queries over Branch and Catalog read models.
    /// </summary>
    public class InventoryItemValidationService : IInventoryItemValidationService
    {

        private const string ActiveStatus = "Active";
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Creates a validation service using the current scope's database context.
        /// </summary>
        /// <param name="context">The context used to query the external read models.</param>
        public InventoryItemValidationService(ApplicationDbContext context)
        {
            _context = context;
        }
        /// <inheritdoc />
        public async Task ValidateStockItemAsync(Guid tenantId, Guid branchId, Guid productId, Guid? productVariantId, decimal quantity, CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            var branchExists = await _context
                .Set<InventoryBranchReadModel>()
                .AnyAsync(
                    branch =>branch.Id == branchId &&branch.TenantId == tenantId &&branch.Status == ActiveStatus,
                    cancellationToken);

            if (!branchExists)
                throw new ApiException(
                    "Branch was not found or is inactive.");

            var product = await _context
                .Set<InventoryProductReadModel>()
                .SingleOrDefaultAsync(product =>product.Id == productId &&product.TenantId == tenantId,
                    cancellationToken);

            if (product == null || product.Status != ActiveStatus)
                throw new ApiException(
                    "Product was not found or is inactive.");

            if (!product.TrackInventory)
                throw new ApiException(
                    "Product does not track inventory.");

            var variants = _context
                .Set<InventoryVariantReadModel>()
                .Where(variant =>
                    variant.TenantId == tenantId &&
                    variant.ProductId == productId);

            if (productVariantId.HasValue)
            {
                var variantExists = await variants.AnyAsync(
                    variant =>
                        variant.Id == productVariantId.Value &&
                        variant.Status == ActiveStatus,
                    cancellationToken);

                if (!variantExists)
                    throw new ApiException(
                        "Variant does not belong to this product or is inactive.");
            }
            else
            {
                var hasVariants = await variants
                    .AnyAsync(cancellationToken);

                if (hasVariants)
                    throw new ApiException(
                        "Select a variant for this product.");
            }

            var unit = await _context
                .Set<InventoryUnitReadModel>()
                .SingleOrDefaultAsync(
                    unit =>
                        unit.Id == product.UnitId &&
                        (unit.TenantId == tenantId ||
                         unit.TenantId == null),
                    cancellationToken);

            if (unit == null)
                throw new ApiException(
                    "The product's measurement unit is unavailable.");

            if (!unit.IsDecimalAllowed &&
                quantity != decimal.Truncate(quantity))
            {
                throw new ApiException(
                    "This product requires a whole-number quantity.");
            }

        }


        public async Task<Result> ValidateStockItemsAsync(Guid tenantId, Guid branchId, List<StockReservationItemDto> items, CancellationToken cancellationToken)
        {
            if (items == null || items.Count == 0)
                return Result.Failure("At least one item is required.");

            // 1. Confirm the branch is active 
            var branchExists = await _context.Set<InventoryBranchReadModel>().AnyAsync(
                x => x.Id == branchId && x.TenantId == tenantId && x.Status == "Active", cancellationToken);

            if (!branchExists)
                return Result.Failure("Branch was not found or is inactive.");

            // 2. Load every product.
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();

            var products = await _context.Set<InventoryProductReadModel>()
                .Where(x => x.TenantId == tenantId && productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            // 3. Load every variant belonging to those products.
            var variantsByProductId = await _context.Set<InventoryVariantReadModel>()
                .Where(x => x.TenantId == tenantId && productIds.Contains(x.ProductId))
                .GroupBy(x => x.ProductId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

            // 4. Load every distinct unit referenced by those products.
            var unitIds = products.Values.Select(p => p.UnitId).Distinct().ToList();

            var units = await _context.Set<InventoryUnitReadModel>()
                .Where(x => unitIds.Contains(x.Id) && (x.TenantId == tenantId || x.TenantId == null))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            // 5. Validate each requested item.
            foreach (var item in items)
            {
                if (!products.TryGetValue(item.ProductId, out var product) || product.Status != "Active")
                    return Result.Failure($"Product {item.ProductId} was not found or is inactive.");

                variantsByProductId.TryGetValue(item.ProductId, out var productVariants);
                productVariants ??= new List<InventoryVariantReadModel>();

                if (item.ProductVariantId.HasValue)
                {
                    var variant = productVariants.SingleOrDefault(v => v.Id == item.ProductVariantId.Value);
                    if (variant == null || variant.Status != "Active")
                        return Result.Failure(
                            $"Variant does not belong to product {item.ProductId} or is inactive.");
                }
                else if (productVariants.Count > 0)
                {
                    return Result.Failure($"Select a variant for product {item.ProductId}.");
                }

                if (!units.TryGetValue(product.UnitId, out var unit))
                    return Result.Failure($"The measurement unit for product {item.ProductId} is unavailable.");

                if (!unit.IsDecimalAllowed && item.Quantity != decimal.Truncate(item.Quantity))
                    return Result.Failure($"Product {item.ProductId} requires a whole-number quantity.");

                if (item.Quantity <= 0)
                    return Result.Failure($"Product {item.ProductId}: quantity must be positive.");
            }

            return Result.Success();
        }
    }
}
