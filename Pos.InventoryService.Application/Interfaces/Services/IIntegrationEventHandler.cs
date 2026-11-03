using Pos.InventoryService.Application.Wrappers;

namespace Pos.InventoryService.Application.Interfaces.Services;

public interface IIntegrationEventHandler
{
    string EventType { get; }
    string ConsumerName { get; }

    /// <summary>Validates and stages event effects without saving. The dispatcher must
    /// commit with the inbox only on success and discard the scope on failure.</summary>
    Task<Result> HandleAsync(string payload, CancellationToken cancellationToken);
}
