using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.CreateDraft
{
    public class CreateStockAdjustmentDraftCommandValidator
        : AbstractValidator<CreateStockAdjustmentDraftCommand>
    {
        public CreateStockAdjustmentDraftCommandValidator()
        {
            RuleFor(x => x.BranchId).NotEmpty();

            RuleFor(x => x.Reason)
                .NotEmpty()
                .MaximumLength(300);

            RuleFor(x => x.Items)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .Must(items => items.Count <= 100)
                .WithMessage("An adjustment supports at most 100 items.");

            When(x => x.Items != null, () =>
            {
                RuleForEach(x => x.Items)
                    .NotNull()
                    .ChildRules(item =>
                    {
                        item.RuleFor(x => x.ProductId).NotEmpty();

                        item.RuleFor(x => x.ProductVariantId)
                            .Must(id => !id.HasValue || id.Value != Guid.Empty)
                            .WithMessage(
                                "ProductVariantId must be null or a non-empty GUID.");

                        item.RuleFor(x => x.NewQuantity)
                            .GreaterThanOrEqualTo(0)
                            .PrecisionScale(18, 3, true);
                    });

                RuleFor(x => x.Items)
                    .Must(items =>
                    {
                        var validItems = items
                            .Where(item => item != null)
                            .ToList();

                        return validItems
                            .Select(item =>
                                (item.ProductId, item.ProductVariantId))
                            .Distinct()
                            .Count() == validItems.Count;
                    })
                    .WithMessage(
                        "The same product and variant cannot appear twice.");
            });
        }
    }
}
