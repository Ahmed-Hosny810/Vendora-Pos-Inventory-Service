using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockBalances.Commands.AddOpeningStock;
using Pos.InventoryService.Application.Features.StockReservations.Commands.CreateCommand;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Persistence.Contexts;

namespace Pos.InventoryService.IntegrationTests.Tests;

[Trait("Category", "SqlServer")]
public class SqlServerGuaranteesTests : InventoryTest
{
    [SqlServerFact]
    public async Task BalanceUniqueIndex_RejectsDuplicateNullVariant_ButAllowsDifferentBranch()
    {
        await Fixture.SeedBalanceAsync();
        Func<Task> duplicate = () => Fixture.SeedBalanceAsync();
        await duplicate.Should().ThrowAsync<DbUpdateException>();
        await Fixture.SeedBalanceAsync(branch: Fixture.OtherBranchId);
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(2);
    }

    [SqlServerFact]
    public async Task ActiveAlertUniqueIndex_AllowsOneActiveEpisode_AndResolvedHistory()
    {
        LowStockAlert Alert(string status) => new() { Id = Guid.NewGuid(), TenantId = Fixture.TenantId,
            BranchId = Fixture.BranchId, ProductId = Fixture.ProductId, Status = status, DetectedAt = DateTime.UtcNow };
        await Fixture.WriteAsync(db => { db.Add(Alert(LowStockAlertStatus.Active)); return Task.CompletedTask; });
        Func<Task> duplicate = () => Fixture.WriteAsync(db => { db.Add(Alert(LowStockAlertStatus.Active)); return Task.CompletedTask; });
        await duplicate.Should().ThrowAsync<DbUpdateException>();
        await Fixture.WriteAsync(db => { db.Add(Alert(LowStockAlertStatus.Resolved)); return Task.CompletedTask; });
        (await Fixture.ReadAsync(db => db.LowStockAlerts.CountAsync())).Should().Be(2);
    }

    [SqlServerFact]
    public async Task MovementIdempotencyIndex_RejectsSameItem_AllowsOtherItemsTenantsAndNullKeys()
    {
        var key = Guid.NewGuid();
        StockMovement Movement(Guid tenant, Guid product, Guid? idempotencyKey) => new() { Id = Guid.NewGuid(), TenantId = tenant,
            BranchId = Fixture.BranchId, ProductId = product, MovementType = StockMovementType.Return,
            ReferenceType = StockReferenceType.Return, ReferenceId = Guid.NewGuid(), IdempotencyKey = idempotencyKey };
        await Fixture.WriteAsync(db => { db.Add(Movement(Fixture.TenantId, Fixture.ProductId, key)); return Task.CompletedTask; });
        Func<Task> duplicate = () => Fixture.WriteAsync(db => { db.Add(Movement(Fixture.TenantId, Fixture.ProductId, key)); return Task.CompletedTask; });
        await duplicate.Should().ThrowAsync<DbUpdateException>();
        await Fixture.WriteAsync(db => { db.AddRange(Movement(Fixture.TenantId, Fixture.DecimalProductId, key),
            Movement(Guid.NewGuid(), Fixture.ProductId, key), Movement(Fixture.TenantId, Fixture.ProductId, null),
            Movement(Fixture.TenantId, Fixture.ProductId, null)); return Task.CompletedTask; });
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(5);
    }

    [SqlServerFact]
    public async Task StaleBalanceSave_RollsBackAllBalancesMovementsAlertsAndOutbox()
    {
        await Fixture.SeedBalanceAsync(10);
        await Fixture.SeedBalanceAsync(10, product: Fixture.DecimalProductId);
        using var scope = Fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var balances = await db.StockBalances.ToListAsync();
        // A concurrent transaction wins after this request has loaded its snapshots.
        await Fixture.WriteAsync(async other => (await other.StockBalances.SingleAsync(x => x.ProductId == Fixture.DecimalProductId)).UpdatedAt = DateTime.UtcNow);
        foreach (var balance in balances) balance.QuantityOnHand = 0;
        db.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = Fixture.TenantId, BranchId = Fixture.BranchId,
            ProductId = Fixture.ProductId, MovementType = StockMovementType.Adjustment, ReferenceType = StockReferenceType.StockAdjustment,
            ReferenceId = Guid.NewGuid(), BeforeQty = 10, AfterQty = 0, QuantityDelta = -10 });
        Func<Task> save = () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        await save.Should().ThrowAsync<ConcurrencyConflictException>();
        (await Fixture.ReadAsync(other => other.StockBalances.ToListAsync())).Should().OnlyContain(x => x.QuantityOnHand == 10);
        (await Fixture.ReadAsync(other => other.StockMovements.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(other => other.LowStockAlerts.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(other => other.OutboxMessages.CountAsync())).Should().Be(0);
    }

    [SqlServerFact]
    public async Task TwoReservationTransitions_OnlyOneCanCommit()
    {
        await Fixture.SeedBalanceAsync(10, 3);
        var id = Guid.NewGuid();
        await Fixture.WriteAsync(db => { db.StockReservations.Add(new StockReservation { Id = id, TenantId = Fixture.TenantId,
            BranchId = Fixture.BranchId, ReferenceId = Guid.NewGuid(), ReferenceType = StockReferenceType.Sale,
            Status = StockReservationStatus.Active, ExpiresAt = DateTime.UtcNow.AddMinutes(5) }); return Task.CompletedTask; });
        using var consumingScope = Fixture.Services.CreateScope(); using var releasingScope = Fixture.Services.CreateScope();
        var consumingDb = consumingScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var releasingDb = releasingScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var consume = await consumingDb.StockReservations.SingleAsync(); var release = await releasingDb.StockReservations.SingleAsync();
        var consumedBalance = await consumingDb.StockBalances.SingleAsync(); var releasedBalance = await releasingDb.StockBalances.SingleAsync();
        consume.Status = StockReservationStatus.Consumed; consumedBalance.QuantityOnHand -= 3; consumedBalance.QuantityReserved -= 3;
        release.Status = StockReservationStatus.Released; releasedBalance.QuantityReserved -= 3;
        await consumingScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        Func<Task> loser = () => releasingScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        await loser.Should().ThrowAsync<ConcurrencyConflictException>();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(7); (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockReservations.SingleAsync())).Status.Should().Be(StockReservationStatus.Consumed);
    }

    [SqlServerFact]
    public async Task ConcurrentOpeningRequests_CreateOnlyOneBalanceAndMovement()
    {
        var command = new AddOpeningStockCommand { RequestId = Guid.NewGuid(), BranchId = Fixture.BranchId,
            ProductId = Fixture.ProductId, Quantity = 10, LowStockThreshold = 1 };
        var outcomes = await Task.WhenAll(Fixture.SendAsync(command), Fixture.SendAsync(command));
        outcomes.Should().OnlyContain(x => x.IsSuccess);
        outcomes[0].Value.Should().Be(outcomes[1].Value);
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync())).Should().Be(1);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(1);
    }

    [SqlServerFact]
    public async Task CompetingReservations_CannotOversell_AndRetryRechecksAvailability()
    {
        await Fixture.SeedBalanceAsync(10);
        CreateStockReservationCommand Create() => new() { BranchId = Fixture.BranchId, SaleId = Guid.NewGuid(),
            Items = [new() { ProductId = Fixture.ProductId, Quantity = 7 }] };
        var first = Create(); var second = Create();
        async Task<bool> Reserve(CreateStockReservationCommand request)
        {
            try { return (await Fixture.SendAsync(request)).IsSuccess; }
            catch (ConcurrencyConflictException) { return false; }
        }
        var results = await Task.WhenAll(Reserve(first), Reserve(second));
        results.Count(x => x).Should().Be(1);
        (await Fixture.BalanceAsync()).QuantityReserved.Should().Be(7);
        // A conflict must be retried in a NEW request scope, never with stale tracked balances.
        var retry = await Fixture.SendAsync(results[0] ? second : first);
        retry.IsFailure.Should().BeTrue();
        retry.Errors.Should().Contain(x => x.Contains("Insufficient"));
        (await Fixture.ReadAsync(db => db.StockReservations.CountAsync())).Should().Be(1);
    }
}
