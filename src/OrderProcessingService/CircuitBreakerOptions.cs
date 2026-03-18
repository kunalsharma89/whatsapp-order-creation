namespace OrderProcessingService;

public class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";
    /// <summary>Number of consecutive failures before opening the circuit.</summary>
    public int FailureThreshold { get; set; } = 5;
    /// <summary>Duration (seconds) the circuit stays open before half-open.</summary>
    public int OpenCircuitDurationSeconds { get; set; } = 30;
    /// <summary>Number of retries with exponential backoff before applying circuit breaker.</summary>
    public int RetryCount { get; set; } = 2;
}
