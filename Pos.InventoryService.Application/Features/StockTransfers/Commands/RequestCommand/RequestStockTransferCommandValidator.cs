using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Commands.RequestCommand
{
    public class RequestStockTransferCommandValidator : AbstractValidator<RequestStockTransferCommand>
    {
        public RequestStockTransferCommandValidator()
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

