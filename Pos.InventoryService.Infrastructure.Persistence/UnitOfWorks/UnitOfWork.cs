using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.Service;


namespace Pos.InventoryService.Infrastructure.Persistence.UnitofWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly LowStockAlertEvaluator _lowStockAlerts;

        public UnitOfWork(ApplicationDbContext context, LowStockAlertEvaluator lowStockAlerts)
        {
            _context = context;
            _lowStockAlerts = lowStockAlerts;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _lowStockAlerts.EvaluateChangedBalancesAsync(cancellationToken);
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new ConcurrencyConflictException(exception);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
                      (sql.Number == 2601 || sql.Number == 2627))
            {
                throw new DuplicateStockWriteException(exception);
            }
        }
    }
}
