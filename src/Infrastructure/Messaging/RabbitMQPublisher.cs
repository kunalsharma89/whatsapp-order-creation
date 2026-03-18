using System.Text;
using System.Text.Json;
using Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Contracts;

namespace Infrastructure.Messaging;

public class RabbitMQPublisher : IMessagePublisher, IOrderFailurePublisher, IDisposable
{
    private readonly ILogger<RabbitMQPublisher> _logger;
    private readonly RabbitMQOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMQPublisher(IOptions<RabbitMQOptions> options, ILogger<RabbitMQPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    private IModel EnsureChannel()
    {
        if (_channel is { IsOpen: true })
            return _channel;

        _connection?.Dispose();
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        RabbitMQQueueSetup.DeclareQueues(_channel, _options);
        _logger.LogInformation("RabbitMQ publisher channel (re)created. Host={Host}:{Port}", _options.HostName, _options.Port);
        return _channel;
    }

    public Task PublishOrderReceivedAsync(OrderReceivedEvent evt, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, _jsonOptions));
        lock (_lock)
        {
            var channel = EnsureChannel();
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = evt.MessageId;
            props.CorrelationId = evt.CorrelationId;
            channel.BasicPublish(RabbitMQQueueSetup.OrderProcessingExchange, _options.OrderProcessingQueue, props, body);
        }
        _logger.LogInformation("Published OrderReceivedEvent {MessageId} to {Queue}", evt.MessageId, _options.OrderProcessingQueue);
        return Task.CompletedTask;
    }

    public Task PublishOrderProcessedAsync(OrderProcessedEvent evt, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, _jsonOptions));
        lock (_lock)
        {
            var channel = EnsureChannel();
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            channel.BasicPublish(RabbitMQQueueSetup.OrderProcessedExchange, _options.OrderProcessedQueue, props, body);
        }
        _logger.LogInformation("Published OrderProcessedEvent {OrderId} to {Queue}", evt.OrderId, _options.OrderProcessedQueue);
        return Task.CompletedTask;
    }

    public Task PublishToRetryAsync(OrderReceivedEvent evt, int retryCount, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, _jsonOptions));
        lock (_lock)
        {
            var channel = EnsureChannel();
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = evt.MessageId;
            props.CorrelationId = evt.CorrelationId;
            props.Headers = new Dictionary<string, object?> { { "x-retry-count", retryCount } };

            if (_options.RetryExponentialBackoff && retryCount > 0)
            {
                var delayMs = Math.Min(_options.MaxRetryDelayMs, _options.BaseRetryDelayMs * (1 << retryCount));
                props.Expiration = delayMs.ToString();
            }

            channel.BasicPublish(RabbitMQQueueSetup.OrderRetryExchange, _options.OrderRetryQueue, props, body);
        }
        _logger.LogWarning(
            "Published OrderReceivedEvent {MessageId} to retry queue (attempt {RetryCount}, CorrelationId: {CorrelationId})",
            evt.MessageId, retryCount, evt.CorrelationId);
        return Task.CompletedTask;
    }

    public Task PublishToDlqAsync(OrderReceivedEvent evt, string error, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt, _jsonOptions));
        lock (_lock)
        {
            var channel = EnsureChannel();
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = evt.MessageId;
            props.Headers = new Dictionary<string, object?> { { "x-dlq-error", error } };
            channel.BasicPublish(RabbitMQQueueSetup.OrderDlqExchange, _options.OrderDlq, props, body);
        }
        _logger.LogError("Published OrderReceivedEvent {MessageId} to DLQ: {Error}", evt.MessageId, error);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
