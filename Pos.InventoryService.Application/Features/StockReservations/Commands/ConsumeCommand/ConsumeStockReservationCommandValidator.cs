using FluentValidation;

namespace Pos.InventoryService.Application.Features.StockReservations.Commands.ConsumeCommand
{
    public class ConsumeStockReservationCommandValidator
        : AbstractValidator<ConsumeStockReservationCommand>
    {
        public ConsumeStockReservationCommandValidator()
        {
            RuleFor(x => x.ReservationId)
                .NotEmpty()
                .WithMessage("Reservation ID is required.");
        }
    }
}
