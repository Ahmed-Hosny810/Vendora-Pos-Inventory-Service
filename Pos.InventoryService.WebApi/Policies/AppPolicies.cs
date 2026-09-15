using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using Pos.InventoryService.Application.Common.Constants;


namespace Pos.InventoryService.WebApi.Policies
{
    public static class AppPolicies
    {
        public static IServiceCollection AddAppPolicies(
            this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    InventoryPolicies.TenantRequired,
                    policy => RequireTenant(policy));

                AddRolePolicy(options, InventoryPolicies.View,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin,
                    InventoryRoles.Cashier,
                    InventoryRoles.InventoryStaff);

                AddRolePolicy(options, InventoryPolicies.OpeningStock,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin,
                    InventoryRoles.InventoryStaff);

                AddRolePolicy(options, InventoryPolicies.Adjust,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin,
                    InventoryRoles.InventoryStaff);

                AddRolePolicy(options, InventoryPolicies.Approve,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin);

                AddRolePolicy(options, InventoryPolicies.Transfer,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin,
                    InventoryRoles.InventoryStaff);

                AddRolePolicy(options, InventoryPolicies.Receive,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin,
                    InventoryRoles.InventoryStaff);

                AddRolePolicy(options, InventoryPolicies.ManageThresholds,
                    InventoryRoles.TenantOwner,
                    InventoryRoles.Admin,
                    InventoryRoles.InventoryStaff);

                //  Sales service permissions.
                AddPermissionPolicy(options, InventoryPolicies.Reserve,"inventory.reserve");

                AddPermissionPolicy(options, InventoryPolicies.CommitReservation,
                    "inventory.commit_reservation");

                AddPermissionPolicy(options, InventoryPolicies.RestockReturn,
                    "inventory.restock_return");
            });

            return services;
        }

        private static void RequireTenant(AuthorizationPolicyBuilder policy)
        {
            policy.AuthenticationSchemes.Add(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

            policy.RequireAuthenticatedUser();

            policy.RequireAssertion(context =>
                Guid.TryParse(
                    context.User.FindFirst("tenant_id")?.Value,
                    out var tenantId)
                && tenantId != Guid.Empty);
        }

        private static void AddRolePolicy(
            AuthorizationOptions options,
            string policyName,
            params string[] roles)
        {
            options.AddPolicy(policyName, policy =>
            {
                RequireTenant(policy);
                policy.RequireRole(roles);
            });
        }

        private static void AddPermissionPolicy(
            AuthorizationOptions options,
            string policyName,
            string permission)
        {
            options.AddPolicy(policyName, policy =>
            {
                RequireTenant(policy);
                policy.RequireClaim("permission", permission);
            });
        }
    }
}
