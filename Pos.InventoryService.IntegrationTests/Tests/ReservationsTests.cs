using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Features.StockReservations.Commands.CreateCommand;
using Pos.InventoryService.Application.Features.StockReservations.Commands.ConsumeCommand;
using Pos.InventoryService.Application.Features.StockReservations.Commands.ReleaseCommand;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Constants;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class ReservationsTests : InventoryTest
{
    private CreateStockReservationCommand Create(decimal quantity = 3) => new()
    { BranchId = Fixture.BranchId, SaleId = Guid.NewGuid(), Items = [new() { ProductId = Fixture.ProductId, Quantity = quantity }] };

    [Fact]
    public async Task Create_ReservesOnlyAvailableStock_WithServerExpiry()
    {
        await Fixture.SeedBalanceAsync(10, 2);
        var before = DateTime.UtcNow;
        var command = Create();
        var result = await Fixture.SendAsync(command);
        result.IsSuccess.Should().BeTrue();
        var reservation = await Fixture.ReadAsync(db => db.StockReservations.Include(x => x.Items).SingleAsync());
        reservation.ReferenceId.Should().Be(command.SaleId);
        reservation.Items.Should().ContainSingle();
        reservation.ExpiresAt.Should().BeAfter(before).And.BeBefore(before.AddHours(1));
        var balance = await Fixture.BalanceAsync();
        balance.QuantityOnHand.Should().Be(10);
        balance.QuantityReserved.Should().Be(5);
        balance.AvailableQuantity.Should().Be(5);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Create_DuplicateSale_DoesNotReserveTwice()
    {
        await Fixture.SeedBalanceAsync();
        var command = Create();
        var first = await Fixture.SendAsync(command);
        var retry = await Fixture.SendAsync(command);
        retry.IsSuccess.Should().BeTrue();
        retry.Value.Should().Be(first.Value);
        (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(3);
        (await Fixture.ReadAsync(db => db.StockReservations.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Create_InsufficientSecondItem_DoesNotPersistFirstItem()
    {
        await Fixture.SeedBalanceAsync();
        await Fixture.SeedBalanceAsync(1, product: Fixture.DecimalProductId);
        var command = Create();
        command.Items.Add(new() { ProductId = Fixture.DecimalProductId, Quantity = 2 });
        (await Fixture.SendAsync(command)).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockReservations.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Consume_DeductsBothQuantitiesAndWritesSale_OnlyOnce()
    {
        await Fixture.SeedBalanceAsync(10);
        var create = Create();
        var id = (await Fixture.SendAsync(create)).Value;
        var consume = new ConsumeStockReservationCommand { ReservationId = id };
        (await Fixture.SendAsync(consume)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(consume)).IsSuccess.Should().BeTrue();
        var balance = await Fixture.BalanceAsync();
        balance.QuantityOnHand.Should().Be(7);
        balance.QuantityReserved.Should().Be(0);
        var movement = await Fixture.ReadAsync(db => db.StockMovements.SingleAsync());
        movement.MovementType.Should().Be(StockMovementType.Sale);
        movement.ReferenceId.Should().Be(create.SaleId);
        movement.BeforeQty.Should().Be(10); movement.AfterQty.Should().Be(7); movement.QuantityDelta.Should().Be(-3);
        (await Fixture.SendAsync(new ReleaseStockReservationCommand { ReservationId = id })).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReleaseOrExpire_IsIdempotent_AndPreventsConsumption(bool expire)
    {
        await Fixture.SeedBalanceAsync(10);
        var id = (await Fixture.SendAsync(Create())).Value;
        if (expire) await Fixture.WriteAsync(async db => (await db.StockReservations.SingleAsync()).ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
        for (var i = 0; i < 2; i++)
        {
            using var scope = Fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IStockReservationReleaseService>();
            var result = expire ? await service.ExpireAsync(Fixture.TenantId, id, default) : await service.ReleaseAsync(Fixture.TenantId, id, default);
            result.IsSuccess.Should().BeTrue();
        }
        var balance = await Fixture.BalanceAsync();
        balance.QuantityOnHand.Should().Be(10); balance.QuantityReserved.Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await Fixture.SendAsync(new ConsumeStockReservationCommand { ReservationId = id })).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Expiry_OnlySelectsOverdueActiveReservations_AndIgnoresFreshOnes()
    {
        await Fixture.SeedBalanceAsync();
        var id = (await Fixture.SendAsync(Create())).Value;
        using (var scope = Fixture.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IStockReservationReleaseService>().ExpireAsync(Fixture.TenantId, id, default)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(3);
        await Fixture.WriteAsync(async db => (await db.StockReservations.SingleAsync()).ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
        using var queryScope = Fixture.Services.CreateScope();
        var rows = await queryScope.ServiceProvider.GetRequiredService<IStockReservationRepositoryAsync>().GetOverdueActiveReservationsAsync(DateTime.UtcNow, 10, default);
        rows.Should().ContainSingle(x => x.ReservationId == id && x.TenantId == Fixture.TenantId);
        (await Fixture.SendAsync(new ConsumeStockReservationCommand { ReservationId = id })).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AnotherTenant_CannotConsumeOrReleaseReservation()
    {
        await Fixture.SeedBalanceAsync();
        var id = (await Fixture.SendAsync(Create())).Value;
        Fixture.CurrentUser.TenantId = Guid.NewGuid();
        (await Fixture.SendAsync(new ConsumeStockReservationCommand { ReservationId = id })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new ReleaseStockReservationCommand { ReservationId = id })).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(3);
    }

    [Fact]
    public async Task Consume_MissingSecondBalance_DoesNotSaveFirstDeduction()
    {
        await Fixture.SeedBalanceAsync();
        await Fixture.SeedBalanceAsync(product: Fixture.DecimalProductId);
        var command = Create(); command.Items.Add(new() { ProductId = Fixture.DecimalProductId, Quantity = 2 });
        var id = (await Fixture.SendAsync(command)).Value;
        await Fixture.WriteAsync(async db => db.StockBalances.Remove(await db.StockBalances.SingleAsync(x => x.ProductId == Fixture.DecimalProductId)));
        (await Fixture.SendAsync(new ConsumeStockReservationCommand { ReservationId = id })).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(100);
        (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(3);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }
}
