namespace Shared.Contracts;

/// <summary>
/// Event published when an order has been successfully processed.
/// Consumed by Notification Service to send confirmation.
/// </summary>
public class OrderProcessedEvent : IEvent
{
    public Guid OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ItemsSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
