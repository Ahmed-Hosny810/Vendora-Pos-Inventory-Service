using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Common.Options;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Shared.Services;
using Pos.InventoryService.Infrastructure.Shared.Workers;

namespace Pos.InventoryService.Infrastructure.Shared
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddSharedInfrastructureServices(this IServiceCollection services,IConfiguration configuration)
        {

            services.AddScoped<IStockReservationReleaseService,StockReservationReleaseService>();

            services.Configure<StockReservationExpirationOptions>(configuration.GetSection(nameof(StockReservationExpirationOptions)));

            services.AddHostedService<StockReservationExpirationWorker>();

            return services;
        }
    }
}
