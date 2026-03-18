namespace Shared.Contracts;

/// <summary>
/// Event published when a WhatsApp message is received via webhook.
/// Used for idempotency and tracing via MessageId and CorrelationId.
/// </summary>
public class OrderReceivedEvent : IEvent
{
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
