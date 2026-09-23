using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Common.Options;
using Pos.InventoryService.Application.Interfaces.Publisher;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Shared.Options;
using Pos.InventoryService.Infrastructure.Shared.Publisher;
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

            services.AddHostedService<OutboxPublisherWorker>();

            services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

            services.Configure<RabbitMqOptions>(
                configuration.GetSection(nameof(RabbitMqOptions)));

            services.Configure<OutboxPublisherOptions>(
                configuration.GetSection(nameof(OutboxPublisherOptions)));

            return services;
        }
    }
}
