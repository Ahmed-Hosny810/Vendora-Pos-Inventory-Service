using Pos.InventoryService.Application.Interfaces;
using Pos.InventoryService.Application.Interfaces.Publisher;
using Pos.InventoryService.Application.Interfaces.Repositories;

namespace Pos.InventoryService.Infrastructure.Shared.Services
{
    public class OutboxDispatcher
    {
        private readonly IOutboxRepositoryAsync _outboxRepository;
        private readonly IEventPublisher _eventPublisher;
        private readonly IUnitOfWork _unitOfWork;

        public OutboxDispatcher(
            IOutboxRepositoryAsync outboxRepository,
            IEventPublisher eventPublisher,
            IUnitOfWork unitOfWork)
        {
            _outboxRepository = outboxRepository;
            _eventPublisher = eventPublisher;
            _unitOfWork = unitOfWork;
        }

        public async Task DispatchAsync(
            Guid messageId,
            CancellationToken cancellationToken)
        {
            // 1. Load the tracked outbox message.
            var message = await _outboxRepository.GetByIdAsync(
                messageId, cancellationToken);

            // 2. Skip messages that no longer exist or were already published.
            if (message == null || message.PublishedAt.HasValue)
                return;

            // 3. Publish the stored event and wait for RabbitMQ confirmation.
            await _eventPublisher.PublishAsync(
                message.Id,
                message.EventType,
                message.Payload,
                cancellationToken);

            // 4. Mark it published only after publication succeeds.
            message.PublishedAt = DateTime.UtcNow;

            // 5. Persist the publication time.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
