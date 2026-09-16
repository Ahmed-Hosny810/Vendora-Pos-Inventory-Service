using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockBalances.Commands.AddOpeningStock
{
    public class AddOpeningStockCommandValidator: AbstractValidator<AddOpeningStockCommand>
    {
        public AddOpeningStockCommandValidator()
        {
            RuleFor(x => x.RequestId)
                .NotEmpty()
                .WithMessage("RequestId is required.");

            RuleFor(x => x.BranchId)
                .NotEmpty()
                .WithMessage("BranchId is required.");

            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithMessage("ProductId is required.");

            RuleFor(x => x.ProductVariantId)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithMessage(
                    "ProductVariantId must be null or a non-empty GUID.");

            RuleFor(x => x.Quantity)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0)
                .WithMessage("Opening quantity must be greater than zero.")
                .PrecisionScale(18, 3, true)
                .WithMessage(
                    "Quantity must fit decimal(18,3): " +
                    "at most 15 integer digits and 3 decimal places.");

            RuleFor(x => x.LowStockThreshold)
                .Cascade(CascadeMode.Stop)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Low-stock threshold cannot be negative.")
                .PrecisionScale(18, 3, true)
                .WithMessage(
                    "Low-stock threshold must fit decimal(18,3): " +
                    "at most 15 integer digits and 3 decimal places.");
        }
    }
}
