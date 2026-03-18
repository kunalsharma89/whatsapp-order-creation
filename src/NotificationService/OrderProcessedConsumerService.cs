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
        await EnsureConnectionAsync(stoppingToken);
        if (_channel == null) return;

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
        _logger.LogInformation("Notification consumer started, listening on {Queue}", _options.OrderProcessedQueue);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            if (_channel?.IsOpen != true)
                await EnsureConnectionAsync(stoppingToken);
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
                AutomaticRecoveryEnabled = true
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            RabbitMQQueueSetup.DeclareQueues(_channel, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ. Retrying in 5s.");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
