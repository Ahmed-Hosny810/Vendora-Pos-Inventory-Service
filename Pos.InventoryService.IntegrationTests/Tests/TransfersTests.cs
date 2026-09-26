using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.CreateDraftCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.UpdateDraftCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.RequestCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.ApproveCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.CancelCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.DispatchCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.ReceiveCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetDetails;
using Pos.InventoryService.Application.Features.StockTransfers.Queries.GetAllQuery;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class TransfersTests : InventoryTest
{
    private CreateStockTransferDraftCommand Create() => new() { FromBranchId = Fixture.BranchId, ToBranchId = Fixture.OtherBranchId,
        Items = [new() { ProductId = Fixture.ProductId, Quantity = 10 }] };
    private Task<StockTransfer> Load(Guid id) => Fixture.ReadAsync(db => db.StockTransfers.Include(x => x.Items).SingleAsync(x => x.Id == id));
    private async Task<StockTransfer> Approved()
    {
        var created = await Fixture.SendAsync(Create()); created.IsSuccess.Should().BeTrue();
        var transfer = await Load(created.Value);
        (await Fixture.SendAsync(new RequestStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsSuccess.Should().BeTrue();
        transfer = await Load(transfer.Id);
        (await Fixture.SendAsync(new ApproveStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsSuccess.Should().BeTrue();
        return await Load(transfer.Id);
    }

    [Fact]
    public async Task Draft_UpdateReplacesItems_AndRequestApprovalDoNotMoveStock()
    {
        await Fixture.SeedBalanceAsync();
        var id = (await Fixture.SendAsync(Create())).Value;
        var draft = await Load(id);
        (await Fixture.SendAsync(new UpdateStockTransferDraftCommand { TransferId = id, RowVersion = draft.RowVersion,
            Items = [new() { ProductId = Fixture.DecimalProductId, Quantity = 2 }] })).IsSuccess.Should().BeTrue();
        draft = await Load(id); draft.Items.Should().ContainSingle(x => x.ProductId == Fixture.DecimalProductId);
        (await Fixture.SendAsync(new ApproveStockTransferCommand { TransferId = id, RowVersion = draft.RowVersion })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new RequestStockTransferCommand { TransferId = id, RowVersion = draft.RowVersion })).IsSuccess.Should().BeTrue();
        draft = await Load(id);
        (await Fixture.SendAsync(new UpdateStockTransferDraftCommand { TransferId = id, RowVersion = draft.RowVersion,
            Items = [new() { ProductId = Fixture.ProductId, Quantity = 1 }] })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new ApproveStockTransferCommand { TransferId = id, RowVersion = draft.RowVersion })).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(100);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Draft_RequiresDistinctOwnedBranches()
    {
        var command = Create(); command.ToBranchId = command.FromBranchId;
        Func<Task> act = () => Fixture.SendAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
        command.ToBranchId = Guid.NewGuid();
        await act.Should().ThrowAsync<ApiException>();
        (await Fixture.ReadAsync(db => db.StockTransfers.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_DeductsSourceOnce_PreservesReservations_AndPreventsCancellation()
    {
        await Fixture.SeedBalanceAsync(20, 3);
        var transfer = await Approved();
        var command = new DispatchStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion };
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(command)).IsSuccess.Should().BeTrue();
        var balance = await Fixture.BalanceAsync(); balance.QuantityOnHand.Should().Be(10); balance.QuantityReserved.Should().Be(3);
        var movement = await Fixture.ReadAsync(db => db.StockMovements.SingleAsync());
        movement.MovementType.Should().Be(StockMovementType.TransferOut); movement.QuantityDelta.Should().Be(-10);
        transfer = await Load(transfer.Id);
        (await Fixture.SendAsync(new CancelStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsFailure.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync(x => x.BranchId == Fixture.OtherBranchId))).Should().Be(0);
    }

    [Fact]
    public async Task Dispatch_RejectsInsufficientAvailableQuantity()
    {
        await Fixture.SeedBalanceAsync(20, 15);
        var transfer = await Approved();
        (await Fixture.SendAsync(new DispatchStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync()).QuantityOnHand.Should().Be(20);
        (await Load(transfer.Id)).Status.Should().Be(StockTransferStatus.Approved);
    }

    [Fact]
    public async Task Receipt_AddsOnlyNewQuantity_HandlesPartialFullAndDuplicateReceipts()
    {
        await Fixture.SeedBalanceAsync(20);
        var transfer = await Approved();
        (await Fixture.SendAsync(new DispatchStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsSuccess.Should().BeTrue();
        transfer = await Load(transfer.Id);
        var receipt = new ReceiveStockTransferCommand { TransferId = transfer.Id, IdempotencyKey = Guid.NewGuid(), RowVersion = transfer.RowVersion,
            Items = [new() { TransferItemId = transfer.Items.Single().Id, Quantity = 4 }] };
        (await Fixture.SendAsync(receipt)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(receipt)).IsSuccess.Should().BeTrue();
        (await Fixture.BalanceAsync(branch: Fixture.OtherBranchId)).QuantityOnHand.Should().Be(4);
        transfer = await Load(transfer.Id); transfer.Status.Should().Be(StockTransferStatus.PartiallyReceived);
        transfer.Items.Single().ReceivedQuantity.Should().Be(4);
        receipt.IdempotencyKey = Guid.NewGuid(); receipt.RowVersion = transfer.RowVersion; receipt.Items[0].Quantity = 7;
        (await Fixture.SendAsync(receipt)).IsFailure.Should().BeTrue();
        (await Fixture.BalanceAsync(branch: Fixture.OtherBranchId)).QuantityOnHand.Should().Be(4);
        receipt.Items[0].Quantity = 6;
        (await Fixture.SendAsync(receipt)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(receipt)).IsSuccess.Should().BeTrue();
        transfer = await Load(transfer.Id); transfer.Status.Should().Be(StockTransferStatus.Received); transfer.ReceivedAt.Should().NotBeNull();
        (await Fixture.BalanceAsync(branch: Fixture.OtherBranchId)).QuantityOnHand.Should().Be(10);
        var movements = await Fixture.ReadAsync(db => db.StockMovements.Where(x => x.MovementType == StockMovementType.TransferIn).OrderBy(x => x.AfterQty).ToListAsync());
        movements.Select(x => x.QuantityDelta).Should().Equal(4m, 6m);
        movements.Select(x => x.BeforeQty).Should().Equal(0m, 4m);
    }

    [Fact]
    public async Task CancelBeforeDispatch_IsIdempotent()
    {
        var transfer = await Approved();
        var cancel = new CancelStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion };
        (await Fixture.SendAsync(cancel)).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(cancel)).IsSuccess.Should().BeTrue();
        transfer = await Load(transfer.Id);
        (await Fixture.SendAsync(new DispatchStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task QueriesAndMutations_EnforceTenantBoundary()
    {
        var transfer = await Approved();
        (await Fixture.SendAsync(new GetStockTransferDetailsQuery { TransferId = transfer.Id })).IsSuccess.Should().BeTrue();
        (await Fixture.SendAsync(new GetStockTransfersQuery { Parameter = new() { Filter = new() { Status = StockTransferStatus.Approved } } })).TotalCount.Should().Be(1);
        Fixture.CurrentUser.TenantId = Guid.NewGuid();
        (await Fixture.SendAsync(new GetStockTransferDetailsQuery { TransferId = transfer.Id })).IsFailure.Should().BeTrue();
        (await Fixture.SendAsync(new GetStockTransfersQuery())).TotalCount.Should().Be(0);
        (await Fixture.SendAsync(new DispatchStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Receipt_InvalidSecondItem_DoesNotPersistFirstReceipt()
    {
        await Fixture.SeedBalanceAsync(20);
        var transfer = await Approved();
        (await Fixture.SendAsync(new DispatchStockTransferCommand { TransferId = transfer.Id, RowVersion = transfer.RowVersion })).IsSuccess.Should().BeTrue();
        transfer = await Load(transfer.Id);
        var receipt = new ReceiveStockTransferCommand { TransferId = transfer.Id, IdempotencyKey = Guid.NewGuid(), RowVersion = transfer.RowVersion,
            Items = [new() { TransferItemId = transfer.Items.Single().Id, Quantity = 2 }, new() { TransferItemId = Guid.NewGuid(), Quantity = 1 }] };
        (await Fixture.SendAsync(receipt)).IsFailure.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockBalances.CountAsync(x => x.BranchId == Fixture.OtherBranchId))).Should().Be(0);
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync(x => x.MovementType == StockMovementType.TransferIn))).Should().Be(0);
        (await Load(transfer.Id)).Items.Single().ReceivedQuantity.Should().Be(0);
    }

    [Theory]
    [InlineData(StockTransferStatus.Draft)] [InlineData(StockTransferStatus.Requested)]
    [InlineData(StockTransferStatus.Approved)] [InlineData(StockTransferStatus.Cancelled)]
    public async Task Receipt_RejectsTransfersThatWereNotDispatched(string status)
    {
        var transfer = await Approved();
        await Fixture.WriteAsync(async db => (await db.StockTransfers.SingleAsync()).Status = status);
        transfer = await Load(transfer.Id);
        (await Fixture.SendAsync(new ReceiveStockTransferCommand { TransferId = transfer.Id, IdempotencyKey = Guid.NewGuid(), RowVersion = transfer.RowVersion,
            Items = [new() { TransferItemId = transfer.Items.Single().Id, Quantity = 1 }] })).IsFailure.Should().BeTrue();
        (await Fixture.ReadAsync(db => db.StockMovements.CountAsync())).Should().Be(0);
    }
}
