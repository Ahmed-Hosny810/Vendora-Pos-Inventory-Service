
using Pos.InventoryService.Application;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Persistence;
using Pos.InventoryService.Infrastructure.Shared;
using Pos.InventoryService.WebApi.Extensions;
using Pos.InventoryServiceWebApi.Services;
using Serilog;

namespace Pos.InventoryService.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();
            });

            // API Versioning
            builder.Services.AddApiVersioningExtension();


            //-------------------Services Registration-----------------------

            builder.Services.AddPersistenceServices(builder.Configuration);

            builder.Services.AddSharedInfrastructureServices();

            builder.Services.AddApplicationLayer(builder.Configuration);

            builder.Services.AddControllers();

            builder.Services.AddHttpContextAccessor();

            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

            // Swagger (via extension)
            builder.Services.AddSwaggerExtension();

            var app = builder.Build();

            
            if (app.Environment.IsDevelopment())
            {
                app.UseSwaggerExtension();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
