using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;
using Pos.InventoryService.Infrastructure.Persistence.Repositories;
using Pos.InventoryService.Infrastructure.Persistence.Service;
using Pos.InventoryService.Infrastructure.Persistence.UnitofWork;


namespace Pos.InventoryService.Infrastructure.Persistence
{
    public static class InfrastructureServicesRegistration
    {
        public static IServiceCollection AddPersistenceServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => {
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "inventory");
            }));

            services.AddScoped(typeof(IGenericRepositoryAsync<,>), typeof(GenericRepositoryAsync<,>));

            services.AddScoped<IStockBalanceRepositoryAsync, StockBalanceRepositoryAsync>();

            services.AddScoped<IStockMovementRepository, StockMovementRepository>();

            services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();

            services.AddScoped<IStockReservationRepositoryAsync, StockReservationRepositoryAsync>();

            services.AddScoped<IStockTransferRepositoryAsync, StockTransferRepositoryAsync>();

            services.AddScoped<ILowStockAlertRepositoryAsync, LowStockAlertRepositoryAsync>();

            services.AddScoped<IOutboxRepositoryAsync, OutboxRepositoryAsync>();

            services.AddScoped<IInventoryItemValidationService,InventoryItemValidationService>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddScoped<LowStockAlertEvaluator>();

            return services;

        }
    }
}
