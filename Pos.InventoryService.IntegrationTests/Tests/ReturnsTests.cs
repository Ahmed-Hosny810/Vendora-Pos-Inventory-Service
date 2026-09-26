using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class ReturnsTests : InventoryTest
{
    private RestockCustomerReturnCommand Return(bool restock = true) => new() { ReturnId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid(),
        BranchId = Fixture.BranchId, Items = [new() { ProductId = Fixture.ProductId, Quantity = 2, Restock = restock }] };

    [Fact]
    public async Task Restock_IncreasesOnHandOnly_AndDuplicateReturnIsHarmless()
    {
        await Fixture.SeedBalanceAsync(10, 3);
        var command = Return();
        command.Items.Add(new() { ProductId = Fixture.DecimalProductId, Quantity = 1, Restock = false });
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        var balance = await Fixture.BalanceAsync(); balance.QuantityOnHand.Should().Be(12); balance.QuantityReserved.Should().Be(3);
        var movement = await Fixture.ReadAsync(db => db.StockMovements.SingleAsync());
        movement.MovementType.Should().Be(StockMovementType.Return); movement.ReferenceId.Should().Be(command.ReturnId);
        movement.IdempotencyKey.Should().Be(command.IdempotencyKey); movement.BeforeQty.Should().Be(10); movement.AfterQty.Should().Be(12);
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task NonRestockableReturn_CreatesNeitherBalanceNorMovement()
    {
        var command = Return(false);
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Restock_CreatesMissingBalance_AndCombinesRepeatedItems()
    {
        var command = Return(); command.Items.Add(new() { ProductId = Fixture.ProductId, Quantity = 3, Restock = true });
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(5);
        (await Fixture.ReadAsync(db => db.StockMovements.SingleAsync())).QuantityDelta.Should().Be(5);
    }

    [Fact]
    public async Task InvalidNonRestockableItem_RejectsWholeRequest()
    {
        await Fixture.SeedBalanceAsync();
        var command = Return(); command.Items.Add(new() { ProductId = Guid.NewGuid(), Quantity = 1, Restock = false });
        Func<Task> act = () => Fixture.SendAsync(command);
        await act.Should().ThrowAsync<ApiException>();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(100);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Restock_RejectsOverflow_WithoutPartialWrites()
    {
        await Fixture.SeedBalanceAsync(999999999999999.999m);
        (await Fixture.SendAsync(Return())).IsFailure.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Restock_AnotherTenantCannotUseOriginalProducts()
    {
        await Fixture.SeedBalanceAsync();
        Fixture.CurrentUser.TenantId = Guid.NewGuid();
        Func<Task> act = () => Fixture.SendAsync(Return());
        await act.Should().ThrowAsync<ApiException>();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(100);
    }
}
