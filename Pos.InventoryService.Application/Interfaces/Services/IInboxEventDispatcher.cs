using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Interfaces.Services
{
    public interface IInboxEventDispatcher
    {
        Task<Result> DispatchAsync(
            Guid eventId,
            string eventType,
            string payload,
            CancellationToken cancellationToken);
    }
}
