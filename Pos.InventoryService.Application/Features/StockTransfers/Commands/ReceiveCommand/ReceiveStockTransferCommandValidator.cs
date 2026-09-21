using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.ReceiveCommand
{
    public class ReceiveStockTransferCommandValidator
       : AbstractValidator<ReceiveStockTransferCommand>
    {
        public ReceiveStockTransferCommandValidator()
        {
            RuleFor(x => x.TransferId)
                .NotEmpty();

            RuleFor(x => x.IdempotencyKey)
                .NotEmpty();

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
                .WithMessage("A receipt cannot contain more than 100 items.")
                .Must(items => items.All(item => item != null))
                .WithMessage("Receipt items cannot be null.")
                .Must(items => items
                    .Select(item => item.TransferItemId)
                    .Distinct()
                    .Count() == items.Count)
                .WithMessage("Duplicate transfer items are not allowed.");

            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(x => x.TransferItemId)
                    .NotEmpty();

                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0)
                    .PrecisionScale(18, 3, true);
            });
        }
    }
}
