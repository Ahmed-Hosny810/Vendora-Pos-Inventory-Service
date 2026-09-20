using FluentValidation;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery
{
    public class GetStockTransfersQueryValidator : AbstractValidator<GetStockTransfersQuery>
    {
        public GetStockTransfersQueryValidator()
        {
            RuleFor(x => x.Parameter).NotNull();

            When(x => x.Parameter != null, () =>
            {
                RuleFor(x => x.Parameter.PageNumber)
                    .GreaterThan(0)
                    .Must((query, page) =>
                        (long)(page - 1) * query.Parameter.PageSize <= int.MaxValue)
                    .WithMessage("The requested page offset is too large.");

                RuleFor(x => x.Parameter.PageSize).InclusiveBetween(1, 50);
                RuleFor(x => x.Parameter.OrderKey).IsInEnum();

                When(x => x.Parameter.Filter != null, () =>
                {
                    RuleFor(x => x.Parameter.Filter!.FromBranchId)
                        .Must(id => !id.HasValue || id.Value != Guid.Empty)
                        .WithMessage("Source branch ID must be valid when provided.");

                    RuleFor(x => x.Parameter.Filter!.ToBranchId)
                        .Must(id => !id.HasValue || id.Value != Guid.Empty)
                        .WithMessage("Destination branch ID must be valid when provided.");

                    RuleFor(x => x.Parameter.Filter!.TransferNumber).MaximumLength(80);

                    RuleFor(x => x.Parameter.Filter!.Status)
                        .Must(status => string.IsNullOrWhiteSpace(status) ||
                            status.Trim() is StockTransferStatus.Draft
                                or StockTransferStatus.Requested
                                or StockTransferStatus.Approved
                                or StockTransferStatus.Dispatched
                                or StockTransferStatus.PartiallyReceived
                                or StockTransferStatus.Received
                                or StockTransferStatus.Cancelled)
                        .WithMessage("Invalid stock transfer status.");

                    RuleFor(x => x.Parameter.Filter!)
                        .Must(filter => !filter.FromUtc.HasValue ||
                            !filter.ToUtcExclusive.HasValue ||
                            filter.FromUtc.Value < filter.ToUtcExclusive.Value)
                        .WithMessage("FromUtc must be earlier than ToUtcExclusive.");
                });
            });
        }
    }
}

