using FluentValidation;


namespace Pos.InventoryService.Application.Features.StockBalances.Queries.GetStockBatchQuey
{
    public class GetBatchStockAvailabilityQueryValidator : AbstractValidator<GetBatchStockAvailabilityQuery>
    {
        public GetBatchStockAvailabilityQueryValidator()
        {
            RuleFor(x => x.BranchId)
                .NotEmpty()
                .WithMessage("BranchId is required.");

            RuleFor(x => x.Items)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage("Items are required.")
                .NotEmpty()
                .WithMessage("Provide at least one item.")
                .Must(items => items.Count <= 100)
                .WithMessage("Provide no more than 100 items.");

            When(x => x.Items != null, () =>
            {
                RuleForEach(x => x.Items)
                    .NotNull()
                    .WithMessage("Items cannot contain null entries.")
                    .ChildRules(item =>
                    {
                        item.RuleFor(x => x.ProductId)
                            .NotEmpty()
                            .WithMessage("ProductId is required.");

                        item.RuleFor(x => x.ProductVariantId)
                            .Must(id => !id.HasValue || id.Value != Guid.Empty)
                            .WithMessage(
                                "ProductVariantId must be null or a non-empty GUID.");
                    });
            });
        }
    }
}
