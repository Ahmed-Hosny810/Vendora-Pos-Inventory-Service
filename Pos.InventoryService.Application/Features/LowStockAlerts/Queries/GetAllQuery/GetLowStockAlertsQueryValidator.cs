using FluentValidation;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;

public class GetLowStockAlertsQueryValidator : AbstractValidator<GetLowStockAlertsQuery>
{
    public GetLowStockAlertsQueryValidator()
    {
        RuleFor(x => x.Parameter).NotNull();
        When(x => x.Parameter != null, () =>
        {
            RuleFor(x => x.Parameter.PageNumber).GreaterThan(0)
                .Must((query, page) => ((long)page - 1) * query.Parameter.PageSize <= int.MaxValue)
                .WithMessage("The requested page offset is too large.");
            RuleFor(x => x.Parameter.PageSize).InclusiveBetween(1, 50);
            RuleFor(x => x.Parameter.OrderKey).IsInEnum();

            When(x => x.Parameter.Filter != null, () =>
            {
                RuleFor(x => x.Parameter.Filter!.ProductVariantId)
                    .Must(id => !id.HasValue || id.Value != Guid.Empty);
                RuleFor(x => x.Parameter.Filter!.Status)
                    .Must(status => string.IsNullOrWhiteSpace(status) ||
                        status.Trim() is LowStockAlertStatus.Active or LowStockAlertStatus.Resolved)
                    .WithMessage("Invalid low-stock alert status.");
            });
        });
    }
}
