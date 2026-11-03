using Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;
using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Interfaces.Services;

public interface ICustomerReturnRestockService
{
    /// <summary>Stages return stock changes without saving. The caller validates input,
    /// commits only on success, and discards the scope after failure.</summary>
    Task<Result<Guid>> StageRestockAsync(
        Guid tenantId,
        Guid userId,
        Guid returnId,
        Guid branchId,
        Guid idempotencyKey,
        IReadOnlyList<RestockCustomerReturnItemDto> itemsToReturn,
        CancellationToken cancellationToken);
}
