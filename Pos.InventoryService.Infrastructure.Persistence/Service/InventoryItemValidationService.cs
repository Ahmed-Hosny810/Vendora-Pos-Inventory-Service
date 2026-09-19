using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.ReadModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pos.InventoryService.Infrastructure.Persistence.Service
{
    public class InventoryItemValidationService : IInventoryItemValidationService
    {

        private const string ActiveStatus = "Active";
        private readonly ApplicationDbContext _context;

        public InventoryItemValidationService(ApplicationDbContext context)
        {
            _context = context;
        }
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
    }
}
