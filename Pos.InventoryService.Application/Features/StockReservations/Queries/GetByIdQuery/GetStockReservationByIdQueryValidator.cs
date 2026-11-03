using FluentValidation;
namespace Pos.InventoryService.Application.Features.StockReservations.Queries.GetByIdQuery;
public class GetStockReservationByIdQueryValidator : AbstractValidator<GetStockReservationByIdQuery>
{
    public GetStockReservationByIdQueryValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
    }
}

