using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.DispatchCommand
{
    public class DispatchStockTransferCommandValidator : AbstractValidator<DispatchStockTransferCommand>
    {
        public DispatchStockTransferCommandValidator()
        {
            RuleFor(x => x.TransferId)
                .NotEmpty()
                .WithMessage("Transfer ID is required.");

            RuleFor(x => x.RowVersion)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(version => version.Length == 8)
                .WithMessage("A valid row version is required.");
        }
    }
}
