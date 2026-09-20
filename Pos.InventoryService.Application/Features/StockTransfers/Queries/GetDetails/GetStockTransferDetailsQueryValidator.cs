using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockTransfers.Queries.GetDetails
{
    public class GetStockTransferDetailsQueryValidator
        : AbstractValidator<GetStockTransferDetailsQuery>
    {
        public GetStockTransferDetailsQueryValidator()
        {
            RuleFor(x => x.TransferId).NotEmpty();
        }
    }
}

