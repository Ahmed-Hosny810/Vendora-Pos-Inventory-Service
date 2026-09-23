using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Application.Interfaces.Repositories
{
    public interface IOutboxRepositoryAsync:IGenericRepositoryAsync<OutboxMessage,Guid>
    {
        Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
             int batchSize,
             CancellationToken cancellationToken);

        Task<OutboxMessage?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);
    }
}
