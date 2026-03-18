using System.Text;
using System.Text.Json;
using Application;
using Domain;
using Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace OrderProcessingService;

public class OrderConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderConsumerHostedService> _logger;
    private readonly RabbitMQOptions _options;
    private readonly CircuitBreakerOptions _circuitBreakerOptions;
    private readonly ResiliencePipeline<CreateOrderResult> _resiliencePipeline;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly object _channelLock = new();
    private IConnection? _connection;
    private IModel? _channel;

    public OrderConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMQOptions> options,
        IOptions<CircuitBreakerOptions> circuitBreakerOptions,
        ILogger<OrderConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _circuitBreakerOptions = circuitBreakerOptions.Value;
        _logger = logger;
        _resiliencePipeline = BuildResiliencePipeline();
    }

    private ResiliencePipeline<CreateOrderResult> BuildResiliencePipeline()
    {
        var retryOptions = new RetryStrategyOptions<CreateOrderResult>
        {
            MaxRetryAttempts = _circuitBreakerOptions.RetryCount,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = new PredicateBuilder<CreateOrderResult>().Handle<Exception>(),
            OnRetry = args =>
            {
                _logger.LogWarning(args.Outcome.Exception,
                    "Order creation retry {Attempt} after exception", args.AttemptNumber + 1);
                return ValueTask.CompletedTask;
            }
        };
        var circuitOptions = new CircuitBreakerStrategyOptions<CreateOrderResult>
        {
            FailureRatio = 1.0,
            MinimumThroughput = _circuitBreakerOptions.FailureThreshold,
            BreakDuration = TimeSpan.FromSeconds(_circuitBreakerOptions.OpenCircuitDurationSeconds),
            ShouldHandle = new PredicateBuilder<CreateOrderResult>().Handle<Exception>(),
            OnOpened = args =>
            {
                _logger.LogError("Circuit breaker opened after repeated failures. Duration: {Duration}s",
                    _circuitBreakerOptions.OpenCircuitDurationSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                _logger.LogInformation("Circuit breaker closed (recovered)");
                return ValueTask.CompletedTask;
            }
        };
        return new ResiliencePipelineBuilder<CreateOrderResult>()
            .AddRetry(retryOptions)
            .AddCircuitBreaker(circuitOptions)
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry until we have a channel (e.g. RabbitMQ not ready at startup in Docker)
        while (_channel == null && !stoppingToken.IsCancellationRequested)
        {
            await EnsureConnectionAsync(stoppingToken);
            if (_channel == null)
            {
                _logger.LogWarning("RabbitMQ connection failed. Retrying in 5s. Host={Host}:{Port} Queue={Queue}",
                    _options.HostName, _options.Port, _options.OrderProcessingQueue);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        if (_channel == null)
        {
            _logger.LogError("Order consumer exiting: no RabbitMQ channel (cancelled or connection failed).");
            return;
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) => await ProcessMessageAsync(ea, stoppingToken);

        _channel.BasicConsume(queue: _options.OrderProcessingQueue, autoAck: false, consumer: consumer);
        _logger.LogInformation("Order consumer started, listening on queue {Queue} (prefetch={Prefetch})",
            _options.OrderProcessingQueue, _options.PrefetchCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            if (_channel?.IsOpen != true)
            {
                _logger.LogWarning("Channel closed. Reconnecting to RabbitMQ...");
                _channel = null;
                _connection?.Close();
                await EnsureConnectionAsync(stoppingToken);
                if (_channel != null)
                {
                    _channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);
                    var newConsumer = new AsyncEventingBasicConsumer(_channel);
                    newConsumer.Received += async (_, ea) => await ProcessMessageAsync(ea, stoppingToken);
                    _channel.BasicConsume(queue: _options.OrderProcessingQueue, autoAck: false, consumer: newConsumer);
                    _logger.LogInformation("Order consumer reconnected, listening on {Queue}", _options.OrderProcessingQueue);
                }
            }
        }
    }

    private async Task EnsureConnectionAsync(CancellationToken ct)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true
            };
            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port} vhost={VHost}...",
                _options.HostName, _options.Port, _options.VirtualHost);
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            RabbitMQQueueSetup.DeclareQueues(_channel, _options);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);
            _logger.LogInformation("Connected to RabbitMQ. Queue {Queue} declared and prefetch set to {Prefetch}.",
                _options.OrderProcessingQueue, _options.PrefetchCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ at {Host}:{Port}. Will retry.",
                _options.HostName, _options.Port);
            _channel = null;
            _connection?.Close();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        _logger.LogInformation("Consuming message from queue. DeliveryTag={DeliveryTag} BodyLength={Length}",
            ea.DeliveryTag, ea.Body.Length);
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());
        OrderReceivedEvent? evt = null;
        try
        {
            evt = JsonSerializer.Deserialize<OrderReceivedEvent>(body, _jsonOptions);
            if (evt == null)
            {
                _logger.LogWarning("Deserialized message is null. DeliveryTag={DeliveryTag}. Nacking.", ea.DeliveryTag);
                SafeNack(ea.DeliveryTag, false);
                return;
            }

            var retryCount = 0;
            if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers.TryGetValue("x-retry-count", out var countObj))
            {
                if (countObj is byte[] bytes)
                    int.TryParse(Encoding.UTF8.GetString(bytes), out retryCount);
            }

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["MessageId"] = evt.MessageId,
                ["CorrelationId"] = evt.CorrelationId,
                ["RetryCount"] = retryCount
            }))
            {
                await ProcessOrderMessageAsync(evt, body, ea, retryCount, ct);
            }
        }
        catch (BrokenCircuitException)
        {
            if (evt != null)
                _logger.LogWarning("Circuit breaker open. Requeuing message. MessageId={MessageId} CorrelationId={CorrelationId}", evt.MessageId, evt.CorrelationId);
            SafeNack(ea.DeliveryTag, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing message. DeliveryTag={DeliveryTag} MessageId={MessageId}",
                ea.DeliveryTag, evt?.MessageId ?? "unknown");
            try
            {
                var retryCount = 0;
                if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers.TryGetValue("x-retry-count", out var countObj))
                {
                    if (countObj is byte[] bytes)
                        int.TryParse(Encoding.UTF8.GetString(bytes), out retryCount);
                }

                if (evt != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var failurePublisher = scope.ServiceProvider.GetRequiredService<IOrderFailurePublisher>();
                    var failedMessageRepo = scope.ServiceProvider.GetRequiredService<IFailedMessageRepository>();

                    if (retryCount >= _options.MaxRetryCount)
                    {
                        await failurePublisher.PublishToDlqAsync(evt, ex.Message, ct);
                        await failedMessageRepo.AddAsync(new FailedMessage
                        {
                            Id = Guid.NewGuid(),
                            MessageId = evt.MessageId,
                            Payload = Encoding.UTF8.GetString(ea.Body.ToArray()),
                            Error = ex.Message,
                            SourceQueue = _options.OrderProcessingQueue,
                            FailedAt = DateTime.UtcNow
                        }, ct);
                        _logger.LogError("Message sent to DLQ after max retries. MessageId={MessageId} Error={Error}", evt.MessageId, ex.Message);
                    }
                    else
                    {
                        await failurePublisher.PublishToRetryAsync(evt, retryCount + 1, ct);
                        _logger.LogWarning("Message sent to retry queue. MessageId={MessageId} NextRetryCount={RetryCount}", evt.MessageId, retryCount + 1);
                    }
                }
                SafeAck(ea.DeliveryTag);
            }
            catch (Exception ackEx)
            {
                _logger.LogError(ackEx, "Failed to ack or publish to DLQ/retry. DeliveryTag={DeliveryTag}", ea.DeliveryTag);
                SafeNack(ea.DeliveryTag, true);
            }
        }
    }

    private void SafeAck(ulong deliveryTag)
    {
        lock (_channelLock)
        {
            _channel?.BasicAck(deliveryTag, false);
        }
    }

    private void SafeNack(ulong deliveryTag, bool requeue)
    {
        lock (_channelLock)
        {
            _channel?.BasicNack(deliveryTag, false, requeue);
        }
    }

    private async Task ProcessOrderMessageAsync(OrderReceivedEvent evt, string body, BasicDeliverEventArgs ea, int retryCount, CancellationToken ct)
    {
        try
        {

            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var successPublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
            var failurePublisher = scope.ServiceProvider.GetRequiredService<IOrderFailurePublisher>();
            var failedMessageRepo = scope.ServiceProvider.GetRequiredService<IFailedMessageRepository>();

            var result = await _resiliencePipeline.ExecuteAsync(async token =>
                await orderService.CreateOrderAsync(evt, token), ct);

            if (result.Success)
            {
                if (result.OrderId.HasValue && !result.IsDuplicate)
                {
                    var itemsSummary = string.Join(", ", evt.RawText);
                    await successPublisher.PublishOrderProcessedAsync(new OrderProcessedEvent
                    {
                        OrderId = result.OrderId.Value,
                        UserId = evt.UserId,
                        PhoneNumber = evt.PhoneNumber,
                        ItemsSummary = itemsSummary,
                        Status = "Processed",
                        CorrelationId = evt.CorrelationId
                    }, ct);
                    _logger.LogInformation("Order created and notification published. OrderId={OrderId} MessageId={MessageId} IsDuplicate={IsDuplicate}",
                        result.OrderId, evt.MessageId, result.IsDuplicate);
                }
                else
                {
                    _logger.LogInformation("Order already exists (idempotent). OrderId={OrderId} MessageId={MessageId}", result.OrderId, evt.MessageId);
                }
                SafeAck(ea.DeliveryTag);
                return;
            }

            if (result.ErrorMessage != null)
            {
                _logger.LogWarning("Order validation/parse failed, sending to DLQ. MessageId={MessageId} Error={Error}", evt.MessageId, result.ErrorMessage);
                await failurePublisher.PublishToDlqAsync(evt, result.ErrorMessage, ct);
                await failedMessageRepo.AddAsync(new FailedMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = evt.MessageId,
                    Payload = body,
                    Error = result.ErrorMessage,
                    SourceQueue = _options.OrderProcessingQueue,
                    FailedAt = DateTime.UtcNow
                }, ct);
                SafeAck(ea.DeliveryTag);
                return;
            }

            SafeAck(ea.DeliveryTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order message. MessageId={MessageId} CorrelationId={CorrelationId}", evt.MessageId, evt.CorrelationId);
            try
            {
                if (evt != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var failurePublisher = scope.ServiceProvider.GetRequiredService<IOrderFailurePublisher>();
                    var failedMessageRepo = scope.ServiceProvider.GetRequiredService<IFailedMessageRepository>();

                    if (retryCount >= _options.MaxRetryCount)
                    {
                        await failurePublisher.PublishToDlqAsync(evt, ex.Message, ct);
                        await failedMessageRepo.AddAsync(new FailedMessage
                        {
                            Id = Guid.NewGuid(),
                            MessageId = evt.MessageId,
                            Payload = Encoding.UTF8.GetString(ea.Body.ToArray()),
                            Error = ex.Message,
                            SourceQueue = _options.OrderProcessingQueue,
                            FailedAt = DateTime.UtcNow
                        }, ct);
                    }
                    else
                    {
                        await failurePublisher.PublishToRetryAsync(evt, retryCount + 1, ct);
                    }
                }
                SafeAck(ea.DeliveryTag);
            }
            catch (Exception ackEx)
            {
                _logger.LogError(ackEx, "Failed to ack or publish to DLQ/retry");
                SafeNack(ea.DeliveryTag, true);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
