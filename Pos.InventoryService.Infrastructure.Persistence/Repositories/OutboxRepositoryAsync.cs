using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class OutboxRepositoryAsync : GenericRepositoryAsync<OutboxMessage, Guid>, IOutboxRepositoryAsync
    {
        private readonly ApplicationDbContext _context;

        public OutboxRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return await _context.OutboxMessages
                .FirstOrDefaultAsync(x => x.Id == id,cancellationToken);
        }

        public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(int batchSize, CancellationToken cancellationToken)
        {
            return await  _context.OutboxMessages
                .Where(x =>x.PublishedAt ==null)
                .OrderBy(x=>x.OccurredAt)
                .ThenBy(x=>x.Id)
                .Take(batchSize)
                .Select(x=>x.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
