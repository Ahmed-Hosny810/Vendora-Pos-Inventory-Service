using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Behaviours;
using Pos.InventoryService.Application.Common.Options;
using System.Reflection;
using Pos.InventoryService.Application.Features.StockReturns.Services;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Events.Handlers;


namespace Pos.InventoryService.Application
{
    public static class ApplicationServicesRegistrations
    {
        public static void AddApplicationLayer(this IServiceCollection services,IConfiguration configuration)
        {
            services.AddAutoMapper(cfg =>
                cfg.AddMaps(Assembly.GetExecutingAssembly())
            );
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            services.AddScoped<ICustomerReturnRestockService, CustomerReturnRestockService>();
            services.AddScoped<IIntegrationEventHandler, SaleReturnCompletedHandler>();

            services.Configure<StockReservationOptions>(configuration.GetSection(nameof(StockReservationOptions)));

        }
    }
}
