using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.StockMovements.Queries.GetMovementsHistoryQuery;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class HistoryTests : InventoryTest
{
    [Fact]
    public async Task History_FiltersTenantBranchProductVariantTypeAndHalfOpenDateInterval()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var source = Guid.NewGuid();
        StockMovement Movement(DateTime date) => new() { Id = Guid.NewGuid(), TenantId = Fixture.TenantId, BranchId = Fixture.BranchId,
            ProductId = Fixture.VariantProductId, ProductVariantId = Fixture.VariantId, MovementType = StockMovementType.Sale,
            BeforeQty = 10, AfterQty = 8, QuantityDelta = -2, ReferenceType = StockReferenceType.Sale, ReferenceId = source,
            CreatedByUserId = Guid.Parse(Fixture.CurrentUser.UserId!), CreatedAt = date };
        var rows = new[] { Movement(start), Movement(start.AddHours(1)), Movement(end), Movement(start.AddTicks(-1)),
            Movement(start), Movement(start), Movement(start), Movement(start), Movement(start) };
        rows[4].TenantId = Guid.NewGuid(); rows[5].BranchId = Fixture.OtherBranchId;
        rows[6].ProductId = Fixture.ProductId; rows[7].ProductVariantId = Fixture.OtherVariantId;
        rows[8].MovementType = StockMovementType.Return;
        await Fixture.WriteAsync(db => { db.AddRange(rows); return Task.CompletedTask; });
        var result = await Fixture.SendAsync(new GetStockMovementsHistory { Parameter = new() { Filter = new() {
            BranchId = Fixture.BranchId, ProductId = Fixture.VariantProductId, ProductVariantId = Fixture.VariantId,
            MovementType = StockMovementType.Sale, FromUtc = start, ToUtcExclusive = end } } });
        result.TotalCount.Should().Be(2);
        result.Data.Should().OnlyContain(x => x.QuantityDelta == -2 && x.BeforeQty == 10 && x.AfterQty == 8 &&
            x.ReferenceId == source && x.CreatedByUserId == Guid.Parse(Fixture.CurrentUser.UserId!));
    }

    [Fact]
    public async Task History_OptionalMovementTypeAndStablePagination_AreSupported()
    {
        var timestamp = DateTime.UtcNow;
        await Fixture.WriteAsync(db => { for (var i = 0; i < 4; i++) db.Add(new StockMovement { Id = Guid.NewGuid(),
            TenantId = Fixture.TenantId, BranchId = Fixture.BranchId, ProductId = Fixture.ProductId, CreatedAt = timestamp,
            MovementType = StockMovementType.Adjustment, ReferenceType = StockReferenceType.StockAdjustment, ReferenceId = Guid.NewGuid() });
            return Task.CompletedTask; });
        var query = new GetStockMovementsHistory { Parameter = new() { PageSize = 2, Filter = new() { BranchId = Fixture.BranchId } } };
        var first = await Fixture.SendAsync(query); var repeat = await Fixture.SendAsync(query);
        first.TotalCount.Should().Be(4);
        repeat.Data.Select(x => x.ReferenceId).Should().Equal(first.Data.Select(x => x.ReferenceId));
        query.Parameter.PageNumber = 2;
        var next = await Fixture.SendAsync(query);
        next.Data.Select(x => x.ReferenceId).Should().NotIntersectWith(first.Data.Select(x => x.ReferenceId));
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(4);
    }
}
