using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.CancelCommand
{
    public class CancelStockTransferCommandValidator : AbstractValidator<CancelStockTransferCommand>
    {
        public CancelStockTransferCommandValidator()
        {
            RuleFor(x => x.TransferId).NotEmpty();
            RuleFor(x => x.RowVersion)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(version => version.Length == 8)
                .WithMessage("A valid row version is required.");
        }
    }
}

