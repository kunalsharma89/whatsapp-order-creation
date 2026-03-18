namespace Infrastructure.Messaging;

public class RabbitMQOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    public string OrderProcessingQueue { get; set; } = "order-processing";
    public string OrderRetryQueue { get; set; } = "order-retry";
    public string OrderDlq { get; set; } = "order-dlq";
    public string OrderProcessedQueue { get; set; } = "order-processed";
    /// <summary>Fixed delay (ms) used when queue-level TTL is applied; also fallback when exponential backoff is disabled.</summary>
    public int RetryDelayMs { get; set; } = 30000;
    /// <summary>Base delay (ms) for exponential backoff: delay = min(MaxRetryDelayMs, BaseRetryDelayMs * 2^retryCount).</summary>
    public int BaseRetryDelayMs { get; set; } = 1000;
    /// <summary>Maximum delay (ms) for exponential backoff.</summary>
    public int MaxRetryDelayMs { get; set; } = 60000;
    /// <summary>Use exponential backoff for retry queue (per-message TTL). If false, RetryDelayMs is used.</summary>
    public bool RetryExponentialBackoff { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
}
