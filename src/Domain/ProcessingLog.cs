namespace Domain;

public class ProcessingLog
{
    public Guid Id { get; set; }
    public Guid? OrderId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime CreatedAt { get; set; }
}
