using OpenIddict.Abstractions;
using Pos.InventoryService.Application.Interfaces.Services;
using System.Security.Claims;

namespace Pos.InventoryServiceWebApi.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return null;

                return user.FindFirstValue(OpenIddictConstants.Claims.Subject)
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }

        public Guid? TenantId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return null;

                var tenantIdValue = user.FindFirstValue("tenant_id");

                if (string.IsNullOrWhiteSpace(tenantIdValue))
                    return null;

                return Guid.TryParse(tenantIdValue, out var tenantId)
                    ? tenantId
                    : null;
            }
        }

        public string? UserType
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return null;

                return user.FindFirstValue("user_type");
            }
        }

        public IReadOnlyList<string> Roles
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return Array.Empty<string>();

                return user.FindAll(OpenIddictConstants.Claims.Role)
                    .Select(x => x.Value)
                    .ToList();
            }
        }

        public string? AccessToken
        {
            get
            {
                var authorizationHeader =
                    _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

                if (string.IsNullOrWhiteSpace(authorizationHeader))
                    return null;

                const string bearerPrefix = "Bearer ";

                if (!authorizationHeader.StartsWith(
                        bearerPrefix,
                        StringComparison.OrdinalIgnoreCase))
                    return null;

                return authorizationHeader[bearerPrefix.Length..].Trim();
            }
        }
    }
}
