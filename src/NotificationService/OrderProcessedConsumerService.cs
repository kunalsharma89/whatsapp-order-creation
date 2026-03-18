using System.Text;
using System.Text.Json;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace NotificationService;

public class OrderProcessedConsumerService : BackgroundService
{
    private readonly ILogger<OrderProcessedConsumerService> _logger;
    private readonly RabbitMQOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private IConnection? _connection;
    private IModel? _channel;

    public OrderProcessedConsumerService(
        IOptions<RabbitMQOptions> options,
        ILogger<OrderProcessedConsumerService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (_channel == null && !stoppingToken.IsCancellationRequested)
        {
            await EnsureConnectionAsync(stoppingToken);
            if (_channel == null)
            {
                _logger.LogWarning("RabbitMQ connection failed. Retrying in 5s. Host={Host}:{Port} Queue={Queue}",
                    _options.HostName, _options.Port, _options.OrderProcessedQueue);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        if (_channel == null)
        {
            _logger.LogError("Notification consumer exiting: no RabbitMQ channel.");
            return;
        }

        StartConsumer();

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
                    StartConsumer();
            }
        }
    }

    private void StartConsumer()
    {
        if (_channel == null) return;

        _channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<OrderProcessedEvent>(body, _jsonOptions);
                if (evt != null)
                {
                    _logger.LogInformation(
                        "[MOCK WhatsApp] Would send to {Phone}: Your order {OrderId} is confirmed. Items: {Summary}. Status: {Status}",
                        evt.PhoneNumber, evt.OrderId, evt.ItemsSummary, evt.Status);
                }
                _channel?.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OrderProcessedEvent");
                _channel?.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(queue: _options.OrderProcessedQueue, autoAck: false, consumer: consumer);
        _logger.LogInformation("Notification consumer started, listening on {Queue} (prefetch={Prefetch})",
            _options.OrderProcessedQueue, _options.PrefetchCount);
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
                AutomaticRecoveryEnabled = true
            };
            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port} vhost={VHost}...",
                _options.HostName, _options.Port, _options.VirtualHost);
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            RabbitMQQueueSetup.DeclareQueues(_channel, _options);
            _logger.LogInformation("Connected to RabbitMQ. Queue {Queue} ready.", _options.OrderProcessedQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ at {Host}:{Port}. Will retry.",
                _options.HostName, _options.Port);
            _channel = null;
            _connection?.Close();
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
