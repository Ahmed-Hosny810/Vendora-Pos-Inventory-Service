using FluentValidation;


namespace Pos.InventoryService.Application.Features.StockReservations.Commands.ReleaseCommand
{
    public class ReleaseStockReservationCommandValidator : AbstractValidator<ReleaseStockReservationCommand>
    {
        public ReleaseStockReservationCommandValidator()
        {
            RuleFor(x => x.ReservationId)
                .NotEmpty()
                .WithMessage("Reservation ID is required.");
        }
    }
}
