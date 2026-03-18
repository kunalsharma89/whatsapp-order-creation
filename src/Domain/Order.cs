namespace Domain;

public class Order
{
    public Guid Id { get; set; }
    /// <summary>
    /// Idempotency key: same as MessageId from webhook event.
    /// </summary>
    public string ExternalMessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
