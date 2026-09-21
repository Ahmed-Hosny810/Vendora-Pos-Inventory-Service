using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;

public class RestockCustomerReturnCommandValidator : AbstractValidator<RestockCustomerReturnCommand>
{
    public RestockCustomerReturnCommandValidator()
    {
        RuleFor(x => x.ReturnId).NotEmpty();
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty();

        RuleFor(x => x.Items).Cascade(CascadeMode.Stop)
            .NotNull().NotEmpty()
            .Must(items => items.Count <= 100)
            .WithMessage("A return cannot contain more than 100 items.")
            .Must(items => items.All(item => item != null))
            .WithMessage("Return items cannot be null.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId).NotEmpty();
            item.RuleFor(x => x.ProductVariantId)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithMessage("Variant ID must be valid when provided.");
            item.RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(18, 3, true);
        });
    }
}

