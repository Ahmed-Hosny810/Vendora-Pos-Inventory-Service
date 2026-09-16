using FluentValidation;


namespace Pos.InventoryService.Application.Features.StockBalances.Queries.GetProductStock
{
    public class GetProductStockByProductIdQueryValidator:AbstractValidator<GetProductStockByProductIdQuery>
    {
        public GetProductStockByProductIdQueryValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithMessage("Product Id is required.");

            RuleFor(x => x.BranchId)
                .NotEmpty()
                .WithMessage("Branch Id is required.");
        }
    }
}
