using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockBalances.Commands.UpdateThreshold;

public class UpdateStockThresholdCommandValidator : AbstractValidator<UpdateStockThresholdCommand>
{
    public UpdateStockThresholdCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductVariantId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).PrecisionScale(18, 3, true);
        RuleFor(x => x.RowVersion).Cascade(CascadeMode.Stop).NotNull()
            .Must(version => version.Length == 8)
            .WithMessage("A valid row version is required.");
    }
}

