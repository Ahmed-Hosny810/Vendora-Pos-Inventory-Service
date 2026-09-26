using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.CreateDraft;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.UpdateDraft;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.ApproveAdjustmentCommand;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.CancelAdjustmentCommand;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.PostAdjustmentCommand;
using Pos.InventoryService.Application.Features.StockAdjustments.Queries.GetDetails;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class AdjustmentsTests : InventoryTest
{
    private async Task<StockAdjustment> Draft(decimal count = 8)
    {
        var result = await Fixture.SendAsync(new CreateStockAdjustmentDraftCommand { BranchId = Fixture.BranchId,
            Reason = " Count correction ", Items = [new() { ProductId = Fixture.ProductId, NewQuantity = count }] });
        result.IsSuccess.Should().BeTrue();
        return await Load(result.Value);
    }
    private Task<StockAdjustment> Load(Guid id) => Fixture.ReadAsync(db => db.StockAdjustments.Include(x => x.Items).SingleAsync(x => x.Id == id));
    private async Task<StockAdjustment> Approve(StockAdjustment draft)
    {
        (await Fixture.SendAsync(new ApproveStockAdjustmentCommand { AdjustmentId = draft.Id, RowVersion = draft.RowVersion })).IsSuccess.Should().BeTrue();
        return await Load(draft.Id);
    }

    [Fact]
    public async Task DraftAndDetails_CaptureCountWithoutChangingStock()
    {
        var balance = await Fixture.SeedBalanceAsync(10);
        var draft = await Draft();
        draft.Reason.Should().Be("Count correction");
        draft.Items.Single().OldQuantity.Should().Be(10);
        draft.Items.Single().NewQuantity.Should().Be(8);
        draft.Items.Single().QuantityDelta.Should().Be(-2);
        draft.Items.Single().BalanceRowVersionAtCount.Should().Equal(balance.RowVersion);
        (await Fixture.SendAsync(new GetStockAdjustmentDetailsQuery { AdjustmentId = draft.Id })).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(10);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Update_ChangesReasonAndItems_PreservesExistingCountSnapshot()
    {
        await Fixture.SeedBalanceAsync(10);
        await Fixture.SeedBalanceAsync(20, product: Fixture.DecimalProductId);
        var draft = await Draft();
        var snapshot = draft.Items.Single().BalanceRowVersionAtCount;
        await Fixture.WriteAsync(async db => (await db.StockBalances.SingleAsync(x => x.ProductId == Fixture.ProductId)).QuantityOnHand = 11);
        var command = new UpdateStockAdjustmentDraftCommand { AdjustmentId = draft.Id, RowVersion = draft.RowVersion,
            Reason = " Revised count ", Items = [new() { ProductId = Fixture.ProductId, NewQuantity = 9 },
                new() { ProductId = Fixture.DecimalProductId, NewQuantity = 19 }] };
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        draft = await Load(draft.Id);
        draft.Items.Should().HaveCount(2);
        var original = draft.Items.Single(x => x.ProductId == Fixture.ProductId);
        original.OldQuantity.Should().Be(10); original.QuantityDelta.Should().Be(-1);
        original.BalanceRowVersionAtCount.Should().Equal(snapshot);
        draft.Reason.Should().Be("Revised count");
        command.RowVersion = draft.RowVersion; command.Items.RemoveAt(0);
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Load(draft.Id)).Items.Should().ContainSingle(x => x.ProductId == Fixture.DecimalProductId);
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(11);
    }

    [Fact]
    public async Task Post_RequiresApproval_AndAppliesOnce_WithHistory()
    {
        await Fixture.SeedBalanceAsync(10, 2);
        var draft = await Draft();
        (await Fixture.SendAsync(new PostStockAdjustmentCommand { AdjustmentId = draft.Id, RowVersion = draft.RowVersion })).IsFailure.Should().BeTrue();
        var approved = await Approve(draft);
        var post = new PostStockAdjustmentCommand { AdjustmentId = approved.Id, RowVersion = approved.RowVersion };
        (await Fixture.SendAsync(post)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(post)).IsSuccess.Should().BeTrue();
        var balance = await Fixture.BalanceAsync();
        balance.QuantityOnHand.Should().Be(8); balance.QuantityReserved.Should().Be(2);
        var movement = await Fixture.ReadAsync(db => db.StockMovements.SingleAsync());
        movement.MovementType.Should().Be(StockMovementType.Adjustment);
        movement.BeforeQty.Should().Be(10); movement.AfterQty.Should().Be(8); movement.QuantityDelta.Should().Be(-2);
        var posted = await Load(draft.Id);
        posted.Status.Should().Be(StockAdjustmentStatus.Posted);
        (await Fixture.SendAsync(new CancelStockAdjustmentCommand { AdjustmentId = posted.Id, RowVersion = posted.RowVersion })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new UpdateStockAdjustmentDraftCommand { AdjustmentId = posted.Id, RowVersion = posted.RowVersion,
            Reason = "Cannot edit", Items = [new() { ProductId = Fixture.ProductId, NewQuantity = 9 }] })).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("staleBalance")] [InlineData("reserved")] [InlineData("staleAdjustment")]
    public async Task Post_RejectsStaleCountsAndReservationConflicts(string conflict)
    {
        await Fixture.SeedBalanceAsync(10, conflict == "reserved" ? 9 : 0);
        var approved = await Approve(await Draft());
        if (conflict == "staleBalance")
            await Fixture.WriteAsync(async db => (await db.StockBalances.SingleAsync()).QuantityOnHand = 11);
        var post = new PostStockAdjustmentCommand { AdjustmentId = approved.Id,
            RowVersion = conflict == "staleAdjustment" ? new byte[8] : approved.RowVersion };
        (await Fixture.SendAsync(post)).IsFailure.Should().BeTrue();
        (await Load(approved.Id)).Status.Should().Be(StockAdjustmentStatus.Approved);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task ZeroDelta_PostsWithoutUnnecessaryMovement()
    {
        await Fixture.SeedBalanceAsync(10);
        var approved = await Approve(await Draft(10));
        (await Fixture.SendAsync(new PostStockAdjustmentCommand { AdjustmentId = approved.Id, RowVersion = approved.RowVersion })).IsSuccess.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancel_DraftOrApproved_IsHarmlessWhenRepeated(bool approve)
    {
        await Fixture.SeedBalanceAsync();
        var adjustment = await Draft();
        if (approve) adjustment = await Approve(adjustment);
        var command = new CancelStockAdjustmentCommand { AdjustmentId = adjustment.Id, RowVersion = adjustment.RowVersion };
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(100);
    }

    [Fact]
    public async Task OtherTenant_CannotReadApproveOrPostAdjustment()
    {
        await Fixture.SeedBalanceAsync();
        var draft = await Draft();
        Fixture.CurrentUser.TenantId = Guid.NewGuid();
        (await Fixture.SendAsync(new GetStockAdjustmentDetailsQuery { AdjustmentId = draft.Id })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new ApproveStockAdjustmentCommand { AdjustmentId = draft.Id, RowVersion = draft.RowVersion })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new PostStockAdjustmentCommand { AdjustmentId = draft.Id, RowVersion = draft.RowVersion })).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Post_OneStaleItem_PreventsAllItemChanges()
    {
        await Fixture.SeedBalanceAsync(10);
        await Fixture.SeedBalanceAsync(20, product: Fixture.DecimalProductId);
        var result = await Fixture.SendAsync(new CreateStockAdjustmentDraftCommand { BranchId = Fixture.BranchId, Reason = "Stock count",
            Items = [new() { ProductId = Fixture.ProductId, NewQuantity = 8 }, new() { ProductId = Fixture.DecimalProductId, NewQuantity = 18 }] });
        var approved = await Approve(await Load(result.Value));
        await Fixture.WriteAsync(async db => (await db.StockBalances.SingleAsync(x => x.ProductId == Fixture.DecimalProductId)).QuantityOnHand = 21);
        (await Fixture.SendAsync(new PostStockAdjustmentCommand { AdjustmentId = approved.Id, RowVersion = approved.RowVersion })).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(10);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
        (await Load(approved.Id)).Status.Should().Be(StockAdjustmentStatus.Approved);
    }
}
