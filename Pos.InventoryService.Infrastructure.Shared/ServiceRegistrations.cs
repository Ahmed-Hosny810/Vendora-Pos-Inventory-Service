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

            services.AddScoped<OutboxDispatcher>();

            services.AddOptions<RabbitMqConsumerOptions>()
                .Bind(configuration.GetSection(nameof(RabbitMqConsumerOptions)))
                .Validate(options => !string.IsNullOrWhiteSpace(options.SourceExchangeName) &&
                    !string.IsNullOrWhiteSpace(options.QueueName) &&
                    !string.IsNullOrWhiteSpace(options.DeadLetterExchange) &&
                    !string.IsNullOrWhiteSpace(options.DeadLetterQueue),
                    "Consumer exchange and queue names are required.")
                .Validate(options => options.RoutingKeys != null &&
                    options.RoutingKeys.Length > 0 &&
                    options.RoutingKeys.All(key => !string.IsNullOrWhiteSpace(key)),
                    "At least one consumer routing key is required.")
                .Validate(options => options.PrefetchCount > 0 && options.MaxDeliveryAttempts > 0,
                    "Consumer prefetch count and delivery attempts must be positive.")
                .ValidateOnStart();

            services.AddScoped<IInboxEventDispatcher, InboxEventDispatcher>();
            services.AddHostedService<RabbitMqConsumerWorker>();

            return services;
        }
    }
}
