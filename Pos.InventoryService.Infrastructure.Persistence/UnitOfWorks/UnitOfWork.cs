using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;


namespace Pos.InventoryService.Infrastructure.Persistence.UnitofWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
