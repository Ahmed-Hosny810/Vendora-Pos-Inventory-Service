using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.InventoryService.Application.Features.StockBalances.Commands.UpdateThreshold;
using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetAllQuery;
using Pos.InventoryService.Application.Features.LowStockAlerts.Queries.GetByIdQuery;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Domain.Constants;
using Pos.InventoryService.Domain.Models;
using Pos.InventoryService.Infrastructure.Shared.Services;

namespace Pos.InventoryService.IntegrationTests.Tests;

public class LowStockAndOutboxTests : InventoryTest
{
    private async Task Threshold(decimal value)
    {
        var balance = await Fixture.BalanceAsync();
        (await Fixture.SendAsync(new UpdateStockThresholdCommand { BranchId = Fixture.BranchId, ProductId = Fixture.ProductId,
            LowStockThreshold = value, RowVersion = balance.RowVersion })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Threshold_ActivatesAtEquality_RefreshesWithoutFlood_ResolvesAndReactivates()
    {
        await Fixture.SeedBalanceAsync(10);
        await Threshold(10);
        var first = await Fixture.ReadAsync(db => db.LowStockAlerts.SingleAsync());
        first.Status.Should().Be(LowStockAlertStatus.Active);
        await Threshold(12);
        (await Fixture.ReadAsync(db => db.LowStockAlerts.CountAsync())).Should().Be(1);
        (await Fixture.ReadAsync(db => db.OutboxMessages.CountAsync())).Should().Be(1);
        (await Fixture.ReadAsync(db => db.LowStockAlerts.SingleAsync())).Threshold.Should().Be(12);
        await Threshold(9);
        var resolved = await Fixture.ReadAsync(db => db.LowStockAlerts.SingleAsync());
        resolved.Status.Should().Be(LowStockAlertStatus.Resolved); resolved.ResolvedAt.Should().NotBeNull();
        await Threshold(10);
        var active = await Fixture.ReadAsync(db => db.LowStockAlerts.SingleAsync(x => x.Status == LowStockAlertStatus.Active));
        active.Id.Should().NotBe(first.Id);
        (await Fixture.ReadAsync(db => db.OutboxMessages.CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task ReservedOnlyChange_DoesNotTriggerOnHandAlert()
    {
        await Fixture.SeedBalanceAsync(10, threshold: 5);
        await Fixture.WriteAsync(async db => (await db.StockBalances.SingleAsync()).QuantityReserved = 8, evaluateAlerts: true);
        (await Fixture.ReadAsync(db => db.LowStockAlerts.CountAsync())).Should().Be(0);
        (await Fixture.ReadAsync(db => db.OutboxMessages.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task BatchEvaluation_KeepsBranchVariantAndTenantAlertsSeparate()
    {
        await Fixture.SeedBalanceAsync(10);
        await Fixture.SeedBalanceAsync(10, branch: Fixture.OtherBranchId);
        await Fixture.SeedBalanceAsync(10, product: Fixture.VariantProductId, variant: Fixture.VariantId);
        await Fixture.SeedBalanceAsync(10, product: Fixture.VariantProductId, variant: Fixture.OtherVariantId);
        await Fixture.SeedBalanceAsync(10, tenant: Guid.NewGuid());
        await Fixture.WriteAsync(async db => { foreach (var b in await db.StockBalances.ToListAsync()) b.QuantityOnHand = 5; }, true);
        (await Fixture.ReadAsync(db => db.LowStockAlerts.CountAsync())).Should().Be(5);
        (await Fixture.ReadAsync(db => db.OutboxMessages.CountAsync())).Should().Be(5);
        var page = await Fixture.SendAsync(new GetLowStockAlertsQuery()); page.TotalCount.Should().Be(4);
        var filtered = await Fixture.SendAsync(new GetLowStockAlertsQuery { Parameter = new() { Filter = new() {
            BranchId = Fixture.BranchId, ProductId = Fixture.VariantProductId, ProductVariantId = Fixture.VariantId, Status = LowStockAlertStatus.Active } } });
        filtered.TotalCount.Should().Be(1);
        var id = await Fixture.ReadAsync(db => db.LowStockAlerts.Where(x => x.TenantId != Fixture.TenantId).Select(x => x.Id).SingleAsync());
        (await Fixture.SendAsync(new GetLowStockAlertByIdQuery { AlertId = id })).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task QuantityRecovery_ResolvesAlert_AndEventContainsOriginalSnapshot()
    {
        await Fixture.SeedBalanceAsync(10);
        await Fixture.WriteAsync(async db => (await db.StockBalances.SingleAsync()).QuantityOnHand = 4, true);
        var message = await Fixture.ReadAsync(db => db.OutboxMessages.SingleAsync());
        using var json = JsonDocument.Parse(message.Payload);
        json.RootElement.GetProperty("TenantId").GetGuid().Should().Be(Fixture.TenantId);
        json.RootElement.GetProperty("QuantityOnHand").GetDecimal().Should().Be(4);
        await Fixture.WriteAsync(async db => (await db.StockBalances.SingleAsync()).QuantityOnHand = 6, true);
        (await Fixture.ReadAsync(db => db.LowStockAlerts.SingleAsync())).Status.Should().Be(LowStockAlertStatus.Resolved);
        (await Fixture.ReadAsync(db => db.OutboxMessages.SingleAsync())).Payload.Should().Be(message.Payload);
    }

    [Fact]
    public async Task Dispatcher_BrokerFailureLeavesPending_RetryUsesSameEventId_ThenSkipsPublished()
    {
        await Fixture.SeedBalanceAsync(10); await Threshold(10);
        var message = await Fixture.ReadAsync(db => db.OutboxMessages.SingleAsync());
        Fixture.Publisher.Fail = true;
        Func<Task> dispatch = async () => {
            await using var scope = Fixture.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(message.Id, default);
        };
        await dispatch.Should().ThrowAsync<IOException>();
        (await Fixture.ReadAsync(db => db.OutboxMessages.SingleAsync())).PublishedAt.Should().BeNull();
        Fixture.Publisher.Fail = false;
        await dispatch(); await dispatch();
        Fixture.Publisher.Messages.Should().ContainSingle();
        Fixture.Publisher.Messages.Single().Should().Be((message.Id, message.EventType, message.Payload));
        (await Fixture.ReadAsync(db => db.OutboxMessages.SingleAsync())).PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboxQuery_ExcludesPublished_AndReturnsOldestBatch()
    {
        var oldest = Guid.NewGuid(); var newer = Guid.NewGuid(); var now = DateTime.UtcNow;
        await Fixture.WriteAsync(db => {
            db.OutboxMessages.AddRange(
                new OutboxMessage { Id = oldest, TenantId = Fixture.TenantId, EventType = "Test", Payload = "{}", OccurredAt = now.AddMinutes(-2) },
                new OutboxMessage { Id = newer, TenantId = Fixture.TenantId, EventType = "Test", Payload = "{}", OccurredAt = now },
                new OutboxMessage { Id = Guid.NewGuid(), TenantId = Fixture.TenantId, EventType = "Test", Payload = "{}", OccurredAt = now.AddMinutes(-3), PublishedAt = now });
            return Task.CompletedTask;
        });
        using var scope = Fixture.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepositoryAsync>();
        (await repository.GetPendingIdsAsync(1, default)).Should().Equal(oldest);
        (await repository.GetPendingIdsAsync(10, default)).Should().Equal(oldest, newer);
        await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(Guid.NewGuid(), default);
        Fixture.Publisher.Messages.Should().BeEmpty();
    }
}
