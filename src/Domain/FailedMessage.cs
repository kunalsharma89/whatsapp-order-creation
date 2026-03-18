namespace Domain;

public class FailedMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string SourceQueue { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
}
