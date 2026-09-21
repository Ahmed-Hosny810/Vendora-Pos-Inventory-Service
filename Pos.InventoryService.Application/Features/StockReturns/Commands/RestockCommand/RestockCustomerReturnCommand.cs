using MediatR;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;

public class RestockCustomerReturnCommand : IRequest<Result<Guid>>
{
    public Guid ReturnId { get; set; }
    public Guid BranchId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public List<RestockCustomerReturnItemDto> Items { get; set; } = new();
}

public class RestockCustomerReturnItemDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public bool Restock { get; set; }
}

