using FluentValidation;

namespace Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetByIdQuery;

public class GetLowStockAlertByIdQueryValidator : AbstractValidator<GetLowStockAlertByIdQuery>
{
    public GetLowStockAlertByIdQueryValidator()
    {
        RuleFor(x => x.AlertId).NotEmpty();
    }
}
