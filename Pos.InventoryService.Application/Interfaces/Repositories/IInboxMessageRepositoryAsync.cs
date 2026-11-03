using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IInboxMessageRepositoryAsync:IGenericRepositoryAsync<InboxMessage,Guid>
    {
        Task<bool> ExistsAsync(Guid eventId, string consumerName, CancellationToken ct);
    }
}
