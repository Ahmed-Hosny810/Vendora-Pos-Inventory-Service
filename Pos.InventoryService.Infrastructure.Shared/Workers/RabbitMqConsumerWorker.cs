using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.InventoryService.Application.Interfaces.Services;
using Pos.InventoryService.Infrastructure.Shared.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Pos.InventoryService.Infrastructure.Shared.Workers
{
    public class RabbitMqConsumerWorker : BackgroundService
    {
        private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);

        private readonly RabbitMqOptions _rabbit;
        private readonly RabbitMqConsumerOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitMqConsumerWorker> _logger;

        public RabbitMqConsumerWorker(
            IOptions<RabbitMqOptions> rabbit,
            IOptions<RabbitMqConsumerOptions> options,
            IServiceScopeFactory scopeFactory,
            ILogger<RabbitMqConsumerWorker> logger)
        {
            _rabbit = rabbit.Value;
            _options = options.Value;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            IConnection? connection = null;
            IChannel? channel = null;

            try
            {
                // 1. Connect (retries only until the first success)
                connection = await ConnectWithRetryAsync(stoppingToken);

                connection.ConnectionShutdownAsync += (_, args) =>
                {
                    _logger.LogWarning("RabbitMQ connection lost: {Reason}", args.ReplyText);
                    return Task.CompletedTask;
                };
                connection.RecoverySucceededAsync += (_, _) =>
                {
                    _logger.LogInformation("RabbitMQ connection recovered.");
                    return Task.CompletedTask;
                };

                // 2. Channel, topology, prefetch

                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await DeclareTopologyAsync(channel, stoppingToken);

                await channel.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);

                // 3. Subscribe
                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += (_, ea) => OnMessageAsync(channel, ea, stoppingToken);

                await channel.BasicConsumeAsync(
                    _options.QueueName, autoAck: false, consumer, stoppingToken);

                _logger.LogInformation("Consuming from {Queue}", _options.QueueName);

                // 4. Stay alive until the app shuts down
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // normal shutdown
            }
            finally
            {
                try 
                {
                    if (channel != null) await channel.DisposeAsync(); 
                } catch { }
                try 
                {
                    if (connection != null) await connection.DisposeAsync(); 
                } catch { }
            }
        }

        private async Task<IConnection> ConnectWithRetryAsync(CancellationToken ct)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbit.HostName,
                Port = _rabbit.Port,
                UserName = _rabbit.UserName,
                Password = _rabbit.Password,
                VirtualHost = _rabbit.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            while (true)
            {
                try
                {
                    return await factory.CreateConnectionAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Cannot connect to RabbitMQ. Retrying in {Delay}s.",
                        ConnectRetryDelay.TotalSeconds);
                    await Task.Delay(ConnectRetryDelay, ct);
                }
            }
        }

        private async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
        {
            // Sales' exchange: declared exactly as Sales declares it (topic, durable)
            await channel.ExchangeDeclareAsync(_options.SourceExchangeName, ExchangeType.Topic,
                durable: true, autoDelete: false, cancellationToken: ct);

            // Dead-letter side
            await channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct,
                durable: true, autoDelete: false, cancellationToken: ct);

            await channel.QueueDeclareAsync(_options.DeadLetterQueue,
                durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

            await channel.QueueBindAsync(_options.DeadLetterQueue,
                _options.DeadLetterExchange, routingKey: "dead", cancellationToken: ct);

            // Main queue (quorum + delivery limit + DLX)
            var args = new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-delivery-limit"] = _options.MaxDeliveryAttempts,
                ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = "dead"
            };
            await channel.QueueDeclareAsync(_options.QueueName,
                durable: true, exclusive: false, autoDelete: false,
                arguments: args, cancellationToken: ct);

            foreach (var routingKey in _options.RoutingKeys)
                await channel.QueueBindAsync(_options.QueueName,
                    _options.SourceExchangeName, routingKey, cancellationToken: ct);
        }

        private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
        {
            try
            {
                var props = ea.BasicProperties;

                // A. Validate the envelope
                if (!Guid.TryParse(props.MessageId, out var eventId) ||
                    string.IsNullOrWhiteSpace(props.Type))
                {
                    _logger.LogError("Malformed message (MessageId={Id}, Type={Type}). Dead-lettering.",
                        props.MessageId, props.Type);
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, CancellationToken.None);
                    return;
                }

                var payload = Encoding.UTF8.GetString(ea.Body.Span);

                // B. One DI scope per message => one DbContext per message
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IInboxEventDispatcher>();

                var result = await dispatcher.DispatchAsync(eventId, props.Type, payload, ct);

                // C. Report the outcome to the broker
                if (result.IsSuccess)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
                }
                else
                {
                    _logger.LogError("Event {EventId} ({Type}) failed permanently: {Errors}",
                        eventId, props.Type, string.Join("; ", result.Errors));
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Shutting down mid-message: no ack, the broker redelivers, the inbox dedupes.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transient failure for message {Id}. Requeueing.",
                    ea.BasicProperties.MessageId);

                try 
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, CancellationToken.None); 
                }
                catch (Exception nackEx) 
                {
                    _logger.LogWarning(nackEx, "Nack failed; broker will redeliver.");
                }
            }
        }
    }
}
