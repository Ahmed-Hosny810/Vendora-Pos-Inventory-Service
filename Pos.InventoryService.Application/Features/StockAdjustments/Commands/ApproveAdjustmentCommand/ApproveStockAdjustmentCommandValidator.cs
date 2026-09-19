
using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.ApproveAdjustmentCommand
{
    public class ApproveStockAdjustmentCommandValidator: AbstractValidator<ApproveStockAdjustmentCommand>
    {
        public ApproveStockAdjustmentCommandValidator()
        {
            RuleFor(x => x.AdjustmentId)
                .NotEmpty();

            RuleFor(x => x.RowVersion)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(version => version.Length == 8)
                .WithMessage("A valid row version is required.");
        }
    }
}
