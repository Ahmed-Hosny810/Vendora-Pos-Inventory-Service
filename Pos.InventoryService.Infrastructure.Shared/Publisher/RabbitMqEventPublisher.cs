using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Interfaces.Publisher;
using Pos.InventoryService.Infrastructure.Shared.Options;
using RabbitMQ.Client;
using System.Text;

namespace Pos.InventoryService.Infrastructure.Shared.Publisher
{
    public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
    {
        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly OutboxPublisherOptions _outboxOptions;
        private readonly ConnectionFactory _connectionFactory;

        private readonly SemaphoreSlim _publishLock = new(1, 1);

        private IConnection? _connection;
        private IChannel? _channel;
        public RabbitMqEventPublisher(
             IOptions<RabbitMqOptions> rabbitMqOptions,
             IOptions<OutboxPublisherOptions> outboxOptions)
        {
            _rabbitMqOptions= rabbitMqOptions.Value;
            _outboxOptions = outboxOptions.Value;

            _connectionFactory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.HostName,
                Port = _rabbitMqOptions.Port,
                UserName = _rabbitMqOptions.UserName,
                Password = _rabbitMqOptions.Password,
                VirtualHost = _rabbitMqOptions.VirtualHost,

                //  reconnects on a later publish attempt.
                AutomaticRecoveryEnabled = false
            };
        }

        public async Task PublishAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken)
        {

            var routingKey = eventType switch
            {
                "LowStockDetected" => "inventory.low-stock.detected.v1",
                _ => throw new NotSupportedException(
                    $"Event type '{eventType}' is not supported.")
            };

            // 2. Allow only one caller to use the publishing channel at a time.
            await _publishLock.WaitAsync(cancellationToken);

            try
            {
                using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

                timeoutCts.CancelAfter(
                    TimeSpan.FromSeconds(_outboxOptions.PublishTimeoutSeconds));

                var publishToken = timeoutCts.Token;

                // 3. Initialize the connection and channel when needed.
                await EnsureConnectedAsync(publishToken);

                var body = Encoding.UTF8.GetBytes(payload);

                var properties = new BasicProperties
                {
                    MessageId = eventId.ToString(),
                    Type = eventType,
                    ContentType = "application/json",
                    Persistent = true
                };

                // 5. Publish and wait for confirmation.
                await _channel!.BasicPublishAsync(
                    exchange: _rabbitMqOptions.ExchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: publishToken);

            }
            finally
            {
                // Release the channel for the next caller, even if publishing fails.
                _publishLock.Release();
            }

        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            if (_connection is { IsOpen:true} &&
                _channel is { IsOpen:true})
            {
                return;
            }

            // Remove resources left over from a disconnected connection.
            await DisposeConnectionAsync();

            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync(
                    cancellationToken);

                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                _channel = await _connection.CreateChannelAsync(
                    options: channelOptions,
                    cancellationToken: cancellationToken);

                // Declare topology once for this channel initialization.
                await _channel.ExchangeDeclareAsync(
                    exchange: _rabbitMqOptions.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: cancellationToken);

                // Temporary learning queue. Move queue ownership to the
                // receiving service or deployment setup later.
                await _channel.QueueDeclareAsync(
                    queue: "inventory.low-stock.test",
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);

                await _channel.QueueBindAsync(
                    queue: "inventory.low-stock.test",
                    exchange: _rabbitMqOptions.ExchangeName,
                    routingKey: "inventory.low-stock.detected.v1",
                    arguments: null,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Do not reuse a partially initialized channel.
                await DisposeConnectionAsync();
                throw;
            }

        }

        private async Task DisposeConnectionAsync()
        {
            var channel = _channel;
            var connection = _connection;

            _channel = null;
            _connection = null;

            try
            {
                if (channel != null)
                    await channel.DisposeAsync();
            }
            finally
            {
                if (connection != null)
                    await connection.DisposeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _publishLock.WaitAsync();

            try
            {
                await DisposeConnectionAsync();
            }
            finally
            {
                _publishLock.Release();
            }
        }
    }
}
