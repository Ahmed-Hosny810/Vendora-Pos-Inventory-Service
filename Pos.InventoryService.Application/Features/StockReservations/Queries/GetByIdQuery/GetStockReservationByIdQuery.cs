using MediatR;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockReservations.Queries.GetByIdQuery;

public class StockReservationDetailsDto
{
    public Guid Id { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid BranchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public List<StockReservationItemDetailsDto> Items { get; set; } = new();
}
public class StockReservationItemDetailsDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
}
public class GetStockReservationByIdQuery : IRequest<Result<StockReservationDetailsDto>>
{
    public Guid ReservationId { get; set; }
}
public class GetStockReservationByIdQueryHandler
    : IRequestHandler<GetStockReservationByIdQuery, Result<StockReservationDetailsDto>>
{
    private readonly IStockReservationRepositoryAsync _repository;
    private readonly ICurrentUserService _currentUser;
    public GetStockReservationByIdQueryHandler(IStockReservationRepositoryAsync repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }
    public async Task<Result<StockReservationDetailsDto>> Handle(
        GetStockReservationByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId == Guid.Empty)
            return Result<StockReservationDetailsDto>.Failure("A valid tenant is required.");
        var reservation = await _repository.GetByIdAsync(tenantId.Value, request.ReservationId, cancellationToken);
        if (reservation == null)
            return Result<StockReservationDetailsDto>.Failure("Reservation was not found.");
        return Result<StockReservationDetailsDto>.Success(new StockReservationDetailsDto
        {
            Id = reservation.Id,
            ReferenceId = reservation.ReferenceId,
            BranchId = reservation.BranchId,
            Status = reservation.Status,
            ExpiresAt = reservation.ExpiresAt,
            Items = reservation.Items.OrderBy(i => i.Id).Select(i => new StockReservationItemDetailsDto
            {
                ProductId = i.ProductId,
                ProductVariantId = i.ProductVariantId,
                Quantity = i.Quantity
            }).ToList()
        });
    }
}

