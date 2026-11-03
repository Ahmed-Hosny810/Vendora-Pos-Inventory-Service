using FluentValidation;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Application.Events.Validators;

public class SaleReturnCompletedValidator : AbstractValidator<SaleReturnCompleted>
{
    public SaleReturnCompletedValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ReturnId).NotEmpty();
        RuleFor(x => x.OriginalSaleId).NotEmpty();
        RuleFor(x => x.ReceivingBranchId).NotEmpty();
        RuleFor(x => x.ProcessedByUserId).NotEmpty();
        RuleFor(x => x.OccurredAt).NotEmpty();

        RuleFor(x => x.Items).Cascade(CascadeMode.Stop)
            .NotNull().NotEmpty()
            .Must(items => items.Count <= 100)
            .WithMessage("A return cannot contain more than 100 items.")
            .Must(items => items.All(item => item != null))
            .WithMessage("Return items cannot be null.");

        When(x => x.Items != null && x.Items.All(item => item != null), () =>
        {
            RuleFor(x => x.Items)
                .Must(items => items.Select(item => item.ReturnItemId).Distinct().Count() == items.Count)
                .WithMessage("Return item IDs must be unique.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.ReturnItemId).NotEmpty();
                item.RuleFor(x => x.ProductId).NotEmpty();
                item.RuleFor(x => x.ProductVariantId)
                    .Must(id => !id.HasValue || id.Value != Guid.Empty);
                item.RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(18, 3, true);
                item.RuleFor(x => x.StockCondition)
                    .Must(condition => condition is StockCondition.Sellable
                        or StockCondition.Damaged or StockCondition.Expired)
                    .WithMessage("The stock condition is invalid.");
                item.RuleFor(x => x)
                    .Must(x => !x.Restock || !x.TrackInventory || x.StockCondition == StockCondition.Sellable)
                    .WithMessage("Damaged or expired goods cannot be restocked as sellable stock.");
            });
        });
    }
}
