using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.UpdateDraftCommand
{
    public class UpdateStockTransferDraftCommandValidator : AbstractValidator<UpdateStockTransferDraftCommand>
    {
        public UpdateStockTransferDraftCommandValidator()
        {
            RuleFor(x => x.TransferId).NotEmpty();
            RuleFor(x => x.RowVersion)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(version => version.Length == 8)
                .WithMessage("A valid row version is required.");

            RuleFor(x => x.Items)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .Must(items => items.Count <= 100)
                .WithMessage("A transfer cannot contain more than 100 items.")
                .Must(items => items.All(item => item != null))
                .WithMessage("Transfer items cannot be null.")
                .Must(items => items.Select(item => (item.ProductId, item.ProductVariantId))
                    .Distinct().Count() == items.Count)
                .WithMessage("Duplicate product and variant items are not allowed.");

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
}

