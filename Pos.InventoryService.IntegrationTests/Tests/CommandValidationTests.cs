using FluentAssertions;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Features.StockAdjustments.Commands.CreateDraft;
using Pos.InventoryService.Application.Features.StockReservations.Commands.CreateCommand;
using Pos.InventoryService.Application.Features.StockTransfers.Commands.ReceiveCommand;
using Pos.InventoryService.Application.Features.StockBalances.Commands.UpdateThreshold;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class CommandValidationTests : InventoryTest
{
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1.0001)]
    public async Task Reservation_RejectsInvalidQuantities(decimal quantity)
    {
        Func<Task> act = () => Fixture.SendAsync(new CreateStockReservationCommand { SaleId = Guid.NewGuid(), BranchId = Fixture.BranchId,
            Items = [new() { ProductId = Fixture.ProductId, Quantity = quantity }] });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Reservation_RejectsDuplicateItemsAndOversizedBatch()
    {
        var command = new CreateStockReservationCommand { SaleId = Guid.NewGuid(), BranchId = Fixture.BranchId,
            Items = [new() { ProductId = Fixture.ProductId, Quantity = 1 }, new() { ProductId = Fixture.ProductId, Quantity = 2 }] };
        Func<Task> act = () => Fixture.SendAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
        command.Items = Enumerable.Range(0, 101).Select(_ => new Pos.InventoryService.Application.Features.StockReservations.DTOS.StockReservationItemDto { ProductId = Guid.NewGuid(), Quantity = 1 }).ToList();
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Receipt_RequiresKeyVersionAndUniqueItems()
    {
        var item = Guid.NewGuid();
        var command = new ReceiveStockTransferCommand { TransferId = Guid.NewGuid(), Items = [new() { TransferItemId = item, Quantity = 1 }] };
        Func<Task> act = () => Fixture.SendAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
        command.IdempotencyKey = Guid.NewGuid(); command.RowVersion = new byte[8];
        command.Items.Add(new() { TransferItemId = item, Quantity = 1 });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Adjustment_RequiresReasonAndNonnegativeCount()
    {
        Func<Task> act = () => Fixture.SendAsync(new CreateStockAdjustmentDraftCommand { BranchId = Fixture.BranchId,
            Reason = "", Items = [new() { ProductId = Fixture.ProductId, NewQuantity = -1 }] });
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Threshold_RejectsNegativeValues()
    {
        Func<Task> act = () => Fixture.SendAsync(new UpdateStockThresholdCommand { BranchId = Fixture.BranchId, ProductId = Fixture.ProductId,
            LowStockThreshold = -1, RowVersion = new byte[8] });
        await act.Should().ThrowAsync<ValidationException>();
    }
}
