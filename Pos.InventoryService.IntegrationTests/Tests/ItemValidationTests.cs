using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Persistence.ReadModels;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class ItemValidationTests : InventoryTest
{
    [Theory]
    [InlineData("inactiveBranch")] [InlineData("foreignBranch")]
    [InlineData("inactiveProduct")] [InlineData("foreignProduct")]
    [InlineData("inactiveVariant")] [InlineData("foreignVariant")]
    [InlineData("foreignUnit")]
    public async Task Validation_RejectsInvalidExternalContractData(string scenario)
    {
        var foreign = Guid.NewGuid();
        await Fixture.WriteAsync(async db => {
            if (Fixture.IsSqlServer)
            {
                switch (scenario)
                {
                    case "inactiveBranch": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE branch.Branches SET Status='Inactive' WHERE Id={Fixture.BranchId}"); break;
                    case "foreignBranch": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE branch.Branches SET TenantId={foreign} WHERE Id={Fixture.BranchId}"); break;
                    case "inactiveProduct": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Catalog.Products SET Status='Inactive' WHERE Id={Fixture.VariantProductId}"); break;
                    case "foreignProduct": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Catalog.Products SET TenantId={foreign} WHERE Id={Fixture.VariantProductId}"); break;
                    case "inactiveVariant": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Catalog.ProductVariants SET Status='Inactive' WHERE Id={Fixture.VariantId}"); break;
                    case "foreignVariant": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Catalog.ProductVariants SET TenantId={foreign} WHERE Id={Fixture.VariantId}"); break;
                    case "foreignUnit": await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Catalog.Units SET TenantId={foreign} WHERE Id={Fixture.UnitId}"); break;
                }
            }
            else
            {
                switch (scenario)
                {
                    case "inactiveBranch": (await db.Set<InventoryBranchReadModel>().SingleAsync(x => x.Id == Fixture.BranchId)).Status = "Inactive"; break;
                    case "foreignBranch": (await db.Set<InventoryBranchReadModel>().SingleAsync(x => x.Id == Fixture.BranchId)).TenantId = foreign; break;
                    case "inactiveProduct": (await db.Set<InventoryProductReadModel>().SingleAsync(x => x.Id == Fixture.VariantProductId)).Status = "Inactive"; break;
                    case "foreignProduct": (await db.Set<InventoryProductReadModel>().SingleAsync(x => x.Id == Fixture.VariantProductId)).TenantId = foreign; break;
                    case "inactiveVariant": (await db.Set<InventoryVariantReadModel>().SingleAsync(x => x.Id == Fixture.VariantId)).Status = "Inactive"; break;
                    case "foreignVariant": (await db.Set<InventoryVariantReadModel>().SingleAsync(x => x.Id == Fixture.VariantId)).TenantId = foreign; break;
                    case "foreignUnit": (await db.Set<InventoryUnitReadModel>().SingleAsync(x => x.Id == Fixture.UnitId)).TenantId = foreign; break;
                }
            }
        });
        using var scope = Fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInventoryItemValidationService>();
        Func<Task> act = () => service.ValidateStockItemAsync(Fixture.TenantId, Fixture.BranchId, Fixture.VariantProductId, Fixture.VariantId, 1, default);
        await act.Should().ThrowAsync<ApiException>();
    }

    [Fact]
    public async Task Validation_AcceptsGlobalUnit_AndDoesNotWriteExternalData()
    {
        await Fixture.WriteAsync(async db => {
            if (Fixture.IsSqlServer) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Catalog.Units SET TenantId=NULL WHERE Id={Fixture.UnitId}");
            else (await db.Set<InventoryUnitReadModel>().SingleAsync(x => x.Id == Fixture.UnitId)).TenantId = null;
        });
        using var scope = Fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IInventoryItemValidationService>()
            .ValidateStockItemAsync(Fixture.TenantId, Fixture.BranchId, Fixture.ProductId, null, 1, default);
        var db = scope.ServiceProvider.GetRequiredService<Pos.InventoryService.Infrastructure.Persistence.Contexts.ApplicationDbContext>();
        db.ChangeTracker.HasChanges().Should().BeFalse();
        (await db.Set<InventoryUnitReadModel>().SingleAsync(x => x.Id == Fixture.UnitId)).TenantId.Should().BeNull();
    }
}
