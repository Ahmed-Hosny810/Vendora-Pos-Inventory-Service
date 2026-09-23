
namespace Pos.InventoryService.Application.Interfaces.Publisher
{
    public interface IEventPublisher
    {
        Task PublishAsync(
            Guid eventId,
            string eventType,
            string payload,
            CancellationToken cancellationToken);
    }
}
