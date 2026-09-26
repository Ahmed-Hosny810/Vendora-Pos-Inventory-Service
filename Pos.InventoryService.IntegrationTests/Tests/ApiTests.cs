using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Pos.InventoryService.Application.Common.Constants;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.WebApi.Controllers.V1;
using Pos.InventoryService.WebApi.Extensions;
using Pos.InventoryService.WebApi.MiddleWares;
using Pos.InventoryService.WebApi.Policies;
using Pos.InventoryServiceWebApi.Services;

namespace Pos.InventoryService.IntegrationTests.Tests;

// Real versioned controllers, policies, model binding, handlers, repositories and middleware.
// Only token verification is replaced; no Identity server or real access token is needed.
public class ApiTests : InventoryTest
{
    private async Task<WebApplication> Start()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        foreach (var registration in Fixture.Registrations) ((IServiceCollection)builder.Services).Add(registration);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(StockBalancesController).Assembly);
        builder.Services.AddApiVersioningExtension();
        builder.Services.AddSwaggerExtension();
        builder.Services.AddAppPolicies();
        builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, _ => { });
        var app = builder.Build();
        app.UseMiddleware<ErrorHandlerMiddleware>();
        app.UseAuthentication(); app.UseAuthorization();
        app.UseSwagger(); app.MapControllers();
        await app.StartAsync();
        return app;
    }

    public static IEnumerable<object[]> Endpoints()
    {
        var controllers = typeof(StockBalancesController).Assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(ControllerBase)));
        foreach (var controller in controllers)
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        foreach (var verb in method.GetCustomAttributes<HttpMethodAttribute>())
        {
            var path = "/api/v1/" + controller.Name.Replace("Controller", "") + (verb.Template is null ? "" : "/" + verb.Template);
            path = Regex.Replace(path, @"\{[^}]+\}", "11111111-1111-1111-1111-111111111111");
            yield return [verb.HttpMethods.Single(), path];
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EveryEndpoint_RejectsAnonymousAndUnauthorizedRole(string method, string path)
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        using var anonymous = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        (await client.SendAsync(anonymous)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Unprivileged");
        client.DefaultRequestHeaders.Add("X-Test-Tenant", Fixture.TenantId.ToString());
        using var unauthorized = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        (await client.SendAsync(unauthorized)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(InventoryPolicies.View, InventoryRoles.Cashier, true)]
    [InlineData(InventoryPolicies.Approve, InventoryRoles.Cashier, false)]
    [InlineData(InventoryPolicies.Approve, InventoryRoles.InventoryStaff, false)]
    [InlineData(InventoryPolicies.Approve, InventoryRoles.Admin, true)]
    [InlineData(InventoryPolicies.Adjust, InventoryRoles.InventoryStaff, true)]
    [InlineData(InventoryPolicies.Transfer, InventoryRoles.InventoryStaff, true)]
    [InlineData(InventoryPolicies.Receive, InventoryRoles.InventoryStaff, true)]
    [InlineData(InventoryPolicies.OpeningStock, InventoryRoles.Cashier, false)]
    [InlineData(InventoryPolicies.ManageThresholds, InventoryRoles.Cashier, false)]
    [InlineData(InventoryPolicies.Reserve, InventoryRoles.Cashier, true)]
    [InlineData(InventoryPolicies.CommitReservation, InventoryRoles.Cashier, true)]
    [InlineData(InventoryPolicies.RestockReturn, InventoryRoles.Cashier, true)]
    public async Task RolePolicies_EnforceAgreedRolesAndTenant(string policy, string role, bool allowed)
    {
        await using var app = await Start();
        using var scope = app.Services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity([new(ClaimTypes.Role, role), new("tenant_id", Fixture.TenantId.ToString())], "Test");
        (await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), null, policy)).Succeeded.Should().Be(allowed);
        identity.RemoveClaim(identity.FindFirst("tenant_id")!);
        (await authorization.AuthorizeAsync(new ClaimsPrincipal(identity), null, policy)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task VersionedHistory_OmittedMovementType_IsOptional_AndSwaggerGenerates()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", InventoryRoles.TenantOwner);
        client.DefaultRequestHeaders.Add("X-Test-Tenant", Fixture.TenantId.ToString());
        (await client.GetAsync($"/api/v1/StockMovements?Parameter.Filter.BranchId={Fixture.BranchId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpeningStock_HTTP_ValidatesInputAndEnforcesTenantClaims()
    {
        await using var app = await Start();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", InventoryRoles.TenantOwner);
        client.DefaultRequestHeaders.Add("X-Test-Tenant", Fixture.TenantId.ToString());
        (await client.PostAsJsonAsync("/api/v1/StockBalances/opening-stock", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = new { RequestId = Guid.NewGuid(), Fixture.BranchId, Fixture.ProductId, Quantity = 10, LowStockThreshold = 2 };
        (await client.PostAsJsonAsync("/api/v1/StockBalances/opening-stock", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Remove("X-Test-Tenant"); client.DefaultRequestHeaders.Add("X-Test-Tenant", Guid.NewGuid().ToString());
        (await client.GetAsync($"/api/v1/StockBalances/availability?BranchId={Fixture.BranchId}&ProductId={Fixture.ProductId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public void HistoryAndAlertControllers_ExposeNoMutationEndpoints()
    {
        var endpoints = Endpoints().Where(x => ((string)x[1]).Contains("StockMovements") || ((string)x[1]).Contains("LowStockAlerts"));
        endpoints.Should().NotBeEmpty().And.OnlyContain(x => (string)x[0] == "GET");
    }
}

public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new(ClaimTypes.Role, role.ToString()),
            new("tenant_id", Request.Headers["X-Test-Tenant"].ToString()), new("sub", "22222222-2222-2222-2222-222222222222")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
