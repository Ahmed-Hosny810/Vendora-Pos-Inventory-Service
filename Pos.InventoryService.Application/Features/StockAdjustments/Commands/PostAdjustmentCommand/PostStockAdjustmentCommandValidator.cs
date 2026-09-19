using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Commands.PostAdjustmentCommand
{
    public class PostStockAdjustmentCommandValidator
        : AbstractValidator<PostStockAdjustmentCommand>
    {
        public PostStockAdjustmentCommandValidator()
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
