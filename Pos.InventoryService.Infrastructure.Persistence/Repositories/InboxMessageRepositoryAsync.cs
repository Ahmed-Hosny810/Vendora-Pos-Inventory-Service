
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.Infrastructure.Persistence.Repositories
{
    public class InboxMessageRepositoryAsync : GenericRepositoryAsync<InboxMessage, Guid>, IInboxMessageRepositoryAsync
    {
        private readonly ApplicationDbContext _context;

        public InboxMessageRepositoryAsync(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        public Task<bool> ExistsAsync(Guid eventId, string consumerName, CancellationToken ct)
        {
            return _context.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.EventId == eventId && m.ConsumerName == consumerName, ct);
        }
    }
}
