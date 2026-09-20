using MediatR;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;


namespace Pos.InventoryService.Application.Features.StockReservations.Commands.ReleaseCommand
{
    public class ReleaseStockReservationCommand : IRequest<Result>
    {
        public Guid ReservationId { get; set; }
    }

    public class ReleaseStockReservationCommandHandler
        : IRequestHandler<ReleaseStockReservationCommand, Result>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IStockReservationReleaseService _releaseService;

        public ReleaseStockReservationCommandHandler(
            ICurrentUserService currentUserService,
            IStockReservationReleaseService releaseService)
        {
            _currentUserService = currentUserService;
            _releaseService = releaseService;
        }

        public async Task<Result> Handle(
            ReleaseStockReservationCommand request,
            CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
                throw new UnauthorizedAccessException(
                    "A valid tenant is required.");

            return await _releaseService.ReleaseAsync(
                tenantId.Value,
                request.ReservationId,
                cancellationToken);
        }
    }
}
