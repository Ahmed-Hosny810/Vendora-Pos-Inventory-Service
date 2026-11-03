using FluentValidation;
using Pos.InventoryService.Application.Events.Constants;
using Pos.InventoryService.Application.Features.StockReturns.Commands.RestockCommand;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using System.Text.Json;

namespace Pos.InventoryService.Application.Events.Handlers;

public class SaleReturnCompletedHandler : IIntegrationEventHandler
{
    // Stable integration identifiers; class renames must not change inbox identity.
    public string EventType => SalesEventTypes.SaleReturnCompleted;
    public string ConsumerName => "inventory.restock-on-sale-return";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IValidator<SaleReturnCompleted> _validator;
    private readonly ICustomerReturnRestockService _restockService;

    public SaleReturnCompletedHandler(
        IValidator<SaleReturnCompleted> validator,
        ICustomerReturnRestockService restockService)
    {
        _validator = validator;
        _restockService = restockService;
    }

    public async Task<Result> HandleAsync(string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(payload))
            return Result.Failure("The return event payload is empty.");

        SaleReturnCompleted? message;

        try
        {
            // Additional Sales fields are ignored for forward-compatible additions.
            message = JsonSerializer.Deserialize<SaleReturnCompleted>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure("The return event payload is invalid JSON.");
        }

        if (message == null)
            return Result.Failure("The return event payload is empty.");

        var validation = await _validator.ValidateAsync(message, cancellationToken);

        if (!validation.IsValid)
            return Result.Failure(validation.Errors.Select(error => error.ErrorMessage).ToArray());

        var items = message.Items
            .Where(item => item.Restock && item.TrackInventory)
            .Select(item => new RestockCustomerReturnItemDto
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                Quantity = item.Quantity,
                Restock = true
            }).ToList();

        // The dispatcher must still record this event in the inbox on success.
        if (items.Count == 0)
            return Result.Success();

        var result = await _restockService.StageRestockAsync(
            tenantId: message.TenantId,
            userId: message.ProcessedByUserId,
            returnId: message.ReturnId,
            branchId: message.ReceivingBranchId,
            idempotencyKey: message.EventId,
            itemsToReturn: items,
            cancellationToken: cancellationToken);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Errors.ToArray());
    }
}
