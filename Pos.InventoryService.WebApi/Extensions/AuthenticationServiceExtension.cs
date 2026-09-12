using OpenIddict.Validation.AspNetCore;

namespace Pos.InventoryService.WebApi.Extensions
{
    public static class AuthenticationServiceExtension
    {
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme =
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });

            services.AddOpenIddict()
                .AddValidation(options =>
                {
                    options.SetIssuer(
                        configuration["Services:Identity:Issuer"]!);

                    options.UseSystemNetHttp();

                    options.UseAspNetCore();
                });

            return services;
        }

    }
}
