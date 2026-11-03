using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.InventoryService.Application.Exceptions;
using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Repositories;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Application.Wrappers;
using Pos.InventoryService.Domain.Models;

namespace Pos.InventoryService.Infrastructure.Shared.Services
{
    public class InboxEventDispatcher : IInboxEventDispatcher
    {
        private sealed record EventMetadata(Guid EventId, Guid TenantId, DateTimeOffset OccurredAt);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly Dictionary<string, IIntegrationEventHandler> _handlers;
        private readonly IInboxMessageRepositoryAsync _inboxMessageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<InboxEventDispatcher> _logger;

        public InboxEventDispatcher(
            IEnumerable<IIntegrationEventHandler> handlers,
            IInboxMessageRepositoryAsync inboxMessageRepository,
            IUnitOfWork unitOfWork,
            ILogger<InboxEventDispatcher> logger)
        {
            _handlers = handlers.ToDictionary(h => h.EventType);
            _inboxMessageRepository = inboxMessageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> DispatchAsync(
            Guid eventId, string eventType, string payload, CancellationToken ct)
        {
            // 1. Find the handler
            if (!_handlers.TryGetValue(eventType, out var handler))
                return Result.Failure($"No handler registered for '{eventType}'.");

            // 2. Validate the common metadata
            EventMetadata? meta;
            try
            {
                meta = JsonSerializer.Deserialize<EventMetadata>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                return Result.Failure("The event payload is not valid JSON.");
            }

            if (meta is null ||
                eventId == Guid.Empty ||
                meta.EventId != eventId ||
                meta.TenantId == Guid.Empty ||
                meta.OccurredAt == default)
            {
                return Result.Failure("Invalid event metadata, or payload EventId does not match MessageId.");
            }

            try
            {
                // 3. Already processed by this consumer?
                if (await _inboxMessageRepository.ExistsAsync(eventId, handler.ConsumerName, ct))
                    return Result.Success();

                // 4. Handler stages the business changes (no SaveChanges inside)
                var result = await handler.HandleAsync(payload, ct);
                if (result.IsFailure)
                    return result;

                // 5. Stage the inbox row in the same context
                await _inboxMessageRepository.AddAsync(new InboxMessage
                {
                    EventId = eventId,
                    ConsumerName = handler.ConsumerName,
                    EventType = eventType,
                    TenantId = meta.TenantId,
                    OccurredAt = meta.OccurredAt.UtcDateTime,
                    ProcessedAt = DateTime.UtcNow
                }, ct);

                // 6. One atomic save: business changes + inbox row
                await _unitOfWork.SaveChangesAsync(ct);

                return Result.Success();
            }
            catch (DuplicateStockWriteException)
            {
                // ExistsAsync reads committed rows only. If the inbox row isn't there,
                // this was a different unique-key failure and must be retried, not ACKed.
                if (!await _inboxMessageRepository.ExistsAsync(eventId, handler.ConsumerName, ct))
                    throw;

                _logger.LogInformation("Event {EventId} was processed concurrently; skipping.", eventId);
                return Result.Success();
            }
        }
    }
}