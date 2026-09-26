using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockBalances.Commands.AddOpeningStock;
using Pos.InventoryService.Application.Features.StockBalances.Commands.UpdateThreshold;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetProductStock;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetBalancesQuery;
using Pos.InventoryService.Application.Features.StockBalances.Queries.GetStockBatchQuey;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class StockBalancesTests : InventoryTest
{
    private AddOpeningStockCommand Opening(decimal quantity = 20) => new()
    { RequestId = Guid.NewGuid(), BranchId = Fixture.BranchId, ProductId = Fixture.ProductId, Quantity = quantity, LowStockThreshold = 5 };

    [Fact]
    public async Task OpeningStock_RecordsBalanceAndBeforeAfterHistory_OnlyOnce()
    {
        var command = Opening();
        var first = await Fixture.SendAsync(command);
        var replay = await Fixture.SendAsync(command);
        first.IsSuccess.Should().BeTrue();
        replay.Value.Should().Be(first.Value);
        var balance = await Fixture.BalanceAsync();
        balance.QuantityOnHand.Should().Be(20);
        balance.QuantityReserved.Should().Be(0);
        var movement = await Fixture.ReadAsync(db => db.StockMovements.SingleAsync());
        movement.MovementType.Should().Be(StockMovementType.OpeningStock);
        movement.BeforeQty.Should().Be(0);
        movement.AfterQty.Should().Be(20);
        movement.QuantityDelta.Should().Be(20);
        movement.ReferenceId.Should().Be(command.RequestId);
        movement.CreatedByUserId.Should().Be(Guid.Parse(Fixture.CurrentUser.UserId!));
    }

    [Fact]
    public async Task OpeningStock_RejectsDuplicateInitializationAndChangedReplay()
    {
        var command = Opening();
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(Opening())).IsFailure.Should().BeTrue();
        command.Quantity++;
        (await Fixture.SendAsync(command)).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(20);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1.0001)]
    public async Task OpeningStock_InvalidQuantity_IsRejectedBeforeWriting(decimal quantity)
    {
        var act = () => Fixture.SendAsync(Opening(quantity));
        await act.Should().ThrowAsync<ValidationException>();
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(0);
    }

    [Theory]
    [InlineData("branch")] [InlineData("product")] [InlineData("nontracked")]
    [InlineData("missingVariant")] [InlineData("wrongVariant")] [InlineData("wholeUnit")]
    public async Task OpeningStock_InvalidOwnershipOrItemRules_AreRejected(string invalid)
    {
        var command = Opening();
        if (invalid == "branch") command.BranchId = Guid.NewGuid();
        if (invalid == "product") command.ProductId = Guid.NewGuid();
        if (invalid == "nontracked") command.ProductId = Fixture.NonTrackedProductId;
        if (invalid == "missingVariant") command.ProductId = Fixture.VariantProductId;
        if (invalid == "wrongVariant") command.ProductVariantId = Fixture.VariantId;
        if (invalid == "wholeUnit") command.Quantity = 1.5m;
        Func<Task> act = () => Fixture.SendAsync(command);
        await act.Should().ThrowAsync<ApiException>();
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task OpeningStock_DecimalUnitsAndSpecificVariants_AreSupported()
    {
        var command = Opening(1.125m);
        command.ProductId = Fixture.DecimalProductId;
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        command = Opening(); command.ProductId = Fixture.VariantProductId; command.ProductVariantId = Fixture.VariantId;
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync(Fixture.DecimalProductId)).QuantityOnHand.Should().Be(1.125m);
        (await Fixture.BalanceAsync(Fixture.VariantProductId, variant: Fixture.VariantId)).QuantityOnHand.Should().Be(20);
    }

    [Fact]
    public async Task SingleAvailability_UsesExactVariantAndTenant_AndComputesAvailable()
    {
        await Fixture.SeedBalanceAsync(20, 3);
        await Fixture.SeedBalanceAsync(9, product: Fixture.ProductId, variant: Fixture.VariantId);
        var query = new GetProductStockByProductIdQuery { BranchId = Fixture.BranchId, ProductId = Fixture.ProductId };
        var result = await Fixture.SendAsync(query);
        result.Value!.AvailableQuantity.Should().Be(17);
        result.Value.ProductVariantId.Should().BeNull();
        query.ProductVariantId = Fixture.VariantId;
        (await Fixture.SendAsync(query)).Value!.QuantityOnHand.Should().Be(9);
        Fixture.CurrentUser.TenantId = Guid.NewGuid();
        (await Fixture.SendAsync(query)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task BatchAvailability_MatchesPairs_AndExcludesOtherBranchesAndTenants()
    {
        await Fixture.SeedBalanceAsync();
        await Fixture.SeedBalanceAsync(product: Fixture.VariantProductId, variant: Fixture.VariantId);
        await Fixture.SeedBalanceAsync(product: Fixture.VariantProductId, variant: Fixture.OtherVariantId);
        await Fixture.SeedBalanceAsync(branch: Fixture.OtherBranchId);
        await Fixture.SeedBalanceAsync(tenant: Guid.NewGuid());
        using var scope = Fixture.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStockBalanceRepositoryAsync>();
        var rows = await repository.GetBatchStockAvailabilityAsync(Fixture.TenantId, Fixture.BranchId,
            [new() { ProductId = Fixture.ProductId },
             new() { ProductId = Fixture.VariantProductId, ProductVariantId = Fixture.VariantId },
             new() { ProductId = Fixture.ProductId, ProductVariantId = Fixture.OtherVariantId }], default);
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(x => x.TenantId == Fixture.TenantId && x.BranchId == Fixture.BranchId);
        rows.Should().NotContain(x => x.ProductVariantId == Fixture.OtherVariantId);
        (await repository.GetBatchStockAvailabilityAsync(Fixture.TenantId, Fixture.BranchId, [], default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Listing_FiltersLowStock_AndPaginatesWithinTenant()
    {
        await Fixture.SeedBalanceAsync(3);
        await Fixture.SeedBalanceAsync(5, product: Fixture.DecimalProductId);
        await Fixture.SeedBalanceAsync(50, product: Fixture.VariantProductId);
        await Fixture.SeedBalanceAsync(1, tenant: Guid.NewGuid());
        var query = new GetStockBalancesQuery { Parameter = new() { PageSize = 1, Filter = new() { LowStockOnly = true } } };
        var page1 = await Fixture.SendAsync(query);
        query.Parameter.PageNumber = 2;
        var page2 = await Fixture.SendAsync(query);
        page1.TotalCount.Should().Be(2);
        page1.Data.Should().ContainSingle();
        page2.Data.Single().ProductId.Should().NotBe(page1.Data.Single().ProductId);
    }

    [Fact]
    public async Task Threshold_RequiresCurrentVersionAndTenant()
    {
        var balance = await Fixture.SeedBalanceAsync();
        var command = new UpdateStockThresholdCommand { BranchId = Fixture.BranchId, ProductId = Fixture.ProductId,
            LowStockThreshold = 100, RowVersion = balance.RowVersion };
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        command.LowStockThreshold = 2;
        (await Fixture.SendAsync(command)).IsFailure.Should().BeTrue();
        command.RowVersion = (await Fixture.BalanceAsync()).RowVersion;
        Fixture.CurrentUser.TenantId = Guid.NewGuid();
        (await Fixture.SendAsync(command)).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).LowStockThreshold.Should().Be(100);
    }

    [Fact]
    public async Task MissingTenant_CannotReadOrInitializeStock()
    {
        Fixture.CurrentUser.TenantId = null;
        (await Fixture.SendAsync(Opening())).IsFailure.Should().BeTrue();
        Func<Task> act = () => Fixture.SendAsync(new GetStockBalancesQuery());
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task BatchQuery_DeduplicatesInputAndMapsAvailability()
    {
        await Fixture.SeedBalanceAsync(10, 4);
        var result = await Fixture.SendAsync(new GetBatchStockAvailabilityQuery { BranchId = Fixture.BranchId,
            Items = [new() { ProductId = Fixture.ProductId }, new() { ProductId = Fixture.ProductId }, new() { ProductId = Guid.NewGuid() }] });
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value!.Single().AvailableQuantity.Should().Be(6);
    }
}
