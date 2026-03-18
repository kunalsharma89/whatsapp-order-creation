using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public static class RabbitMQQueueSetup
{
    public const string OrderProcessingExchange = "order-processing-exchange";
    public const string OrderRetryExchange = "order-retry-exchange";
    public const string OrderDlqExchange = "order-dlq-exchange";
    public const string OrderProcessedExchange = "order-processed-exchange";

    public static void DeclareQueues(RabbitMQ.Client.IModel channel, RabbitMQOptions options)
    {
        channel.ExchangeDeclare(OrderProcessingExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.ExchangeDeclare(OrderRetryExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.ExchangeDeclare(OrderDlqExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.ExchangeDeclare(OrderProcessedExchange, ExchangeType.Direct, durable: true, autoDelete: false);

        var mainQueueArgs = new Dictionary<string, object>();
        channel.QueueDeclare(options.OrderProcessingQueue, durable: true, exclusive: false, autoDelete: false, mainQueueArgs);
        channel.QueueBind(options.OrderProcessingQueue, OrderProcessingExchange, options.OrderProcessingQueue);

        var retryQueueTtl = options.RetryExponentialBackoff ? options.MaxRetryDelayMs : options.RetryDelayMs;
        var retryArgs = new Dictionary<string, object>
        {
            { "x-message-ttl", retryQueueTtl },
            { "x-dead-letter-exchange", OrderProcessingExchange },
            { "x-dead-letter-routing-key", options.OrderProcessingQueue }
        };
        channel.QueueDeclare(options.OrderRetryQueue, durable: true, exclusive: false, autoDelete: false, retryArgs);
        channel.QueueBind(options.OrderRetryQueue, OrderRetryExchange, options.OrderRetryQueue);

        channel.QueueDeclare(options.OrderDlq, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.OrderDlq, OrderDlqExchange, options.OrderDlq);

        channel.QueueDeclare(options.OrderProcessedQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.OrderProcessedQueue, OrderProcessedExchange, options.OrderProcessedQueue);
    }
}
