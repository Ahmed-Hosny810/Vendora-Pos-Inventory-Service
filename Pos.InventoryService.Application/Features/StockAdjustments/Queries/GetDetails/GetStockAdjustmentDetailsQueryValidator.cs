using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockAdjustments.Queries.GetDetails
{
    public class GetStockAdjustmentDetailsQueryValidator
        : AbstractValidator<GetStockAdjustmentDetailsQuery>
    {
        public GetStockAdjustmentDetailsQueryValidator()
        {
            RuleFor(x => x.AdjustmentId).NotEmpty();
        }
    }
}
