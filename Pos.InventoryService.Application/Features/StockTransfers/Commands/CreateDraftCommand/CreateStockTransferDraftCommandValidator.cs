using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.CreateDraftCommand
{
    public class CreateStockTransferDraftCommandValidator
        : AbstractValidator<CreateStockTransferDraftCommand>
    {
        public CreateStockTransferDraftCommandValidator()
        {
            RuleFor(x => x.FromBranchId).NotEmpty();
            RuleFor(x => x.ToBranchId)
                .NotEmpty()
                .NotEqual(x => x.FromBranchId)
                .WithMessage("Source and destination branches must be different.");

            RuleFor(x => x.Items)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .Must(items => items.Count <= 100)
                .WithMessage("A transfer cannot contain more than 100 items.")
                .Must(items => items.All(item => item != null))
                .WithMessage("Transfer items cannot be null.")
                .Must(items => items
                    .Select(item => (item.ProductId, item.ProductVariantId))
                    .Distinct().Count() == items.Count)
                .WithMessage("Duplicate product and variant items are not allowed.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.ProductId).NotEmpty();
                item.RuleFor(x => x.ProductVariantId)
                    .Must(id => !id.HasValue || id.Value != Guid.Empty)
                    .WithMessage("Variant ID must be valid when provided.");
                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0)
                    .PrecisionScale(18, 3, true);
            });
        }
    }
}
