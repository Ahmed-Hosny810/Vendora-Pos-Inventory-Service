
namespace Pos.InventoryService.Application.Interfaces.Services
{
    /// <summary>
    /// Validates branch ownership and inventory item eligibility using Branch and Catalog data.
    /// </summary>
    public interface IInventoryItemValidationService
    {
        /// <summary>
        /// Checks that the branch and product belong to the tenant, are active, and that
        /// the product, variant and measurement unit allow the requested inventory operation.
        /// </summary>
        /// <param name="tenantId">The non-empty tenant ID supplied by the trusted caller.</param>
        /// <param name="branchId">The branch that must belong to the tenant and be active.</param>
        /// <param name="productId">The tenant-owned, active product that must track inventory.</param>
        /// <param name="productVariantId">
        /// The active variant belonging to the product and tenant.
        /// May be null only when the product has no variants.
        /// </param>
        /// <param name="quantity">
        /// The quantity checked against the product's unit rules.
        /// Must be a whole number when the unit does not allow decimals.
        /// </param>
        /// <param name="cancellationToken">The token used to cancel database queries.</param>
        /// <returns>A task that completes successfully when all eligibility checks pass.</returns>
        /// <remarks>
        /// The measurement unit must belong to the tenant or be a shared unit.
        /// Reads Branch and Catalog data without modifying it.
        /// Checks reflect the data observed during validation; they do not lock that data.
        /// The caller remains responsible for authentication, authorization, quantity
        /// positivity and precision, stock balance existence, and available-stock checks.
        /// </remarks>
        /// <exception cref="System.UnauthorizedAccessException">
        /// The tenant ID is empty.
        /// </exception>
        /// <exception cref="Pos.InventoryService.Application.Exceptions.ApiException">
        /// The branch or product is missing, belongs to another tenant, or is inactive;
        /// the product does not track inventory; the variant selection is invalid;
        /// the measurement unit is unavailable; or the quantity violates whole-unit rules.
        /// </exception>
        /// <exception cref="System.OperationCanceledException">
        /// Cancellation is requested while executing a database query.
        /// </exception>
        Task ValidateStockItemAsync(
            Guid tenantId,
            Guid branchId,
            Guid productId,
            Guid? productVariantId,
            decimal quantity,
            CancellationToken cancellationToken);
    }
}
