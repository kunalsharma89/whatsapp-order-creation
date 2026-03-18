using Application;
using Shared.Contracts;
using WebhookService.Models;

namespace WebhookService.Services;

public class WebhookMessageHandler : IWebhookMessageHandler
{
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<WebhookMessageHandler> _logger;

    public WebhookMessageHandler(IMessagePublisher publisher, ILogger<WebhookMessageHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> HandlePayloadAsync(WhatsAppWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        if (payload?.Entry == null || payload.Entry.Count == 0)
        {
            _logger.LogDebug("Webhook payload empty or missing entry");
            return 0;
        }

        var count = 0;
        var correlationId = Guid.NewGuid().ToString("N");
        _logger.LogInformation("Processing webhook payload with CorrelationId {CorrelationId}, EntryCount {EntryCount}",
            correlationId, payload.Entry.Count);

        foreach (var entry in payload.Entry)
        {
            if (entry.Changes == null) continue;
            foreach (var change in entry.Changes)
            {
                var value = change.Value;
                if (value?.Messages == null) continue;
                foreach (var msg in value.Messages)
                {
                    if (msg.Type != "text" || msg.Text?.Body == null) continue;
                    var userId = value.Contacts?.FirstOrDefault()?.WaId ?? msg.From ?? "unknown";
                    var phoneNumber = msg.From ?? userId;
                    var eventToPublish = new OrderReceivedEvent
                    {
                        MessageId = msg.Id ?? Guid.NewGuid().ToString("N"),
                        UserId = userId,
                        PhoneNumber = phoneNumber,
                        RawText = msg.Text.Body.Trim(),
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = correlationId
                    };
                    await _publisher.PublishOrderReceivedAsync(eventToPublish, cancellationToken);
                    count++;
                    _logger.LogInformation(
                        "Published order message. MessageId={MessageId} Phone={PhoneNumber} CorrelationId={CorrelationId} RawTextLength={RawTextLength}",
                        eventToPublish.MessageId, phoneNumber, correlationId, eventToPublish.RawText.Length);
                }
            }
        }

        _logger.LogInformation("Webhook processing completed. CorrelationId={CorrelationId} MessagesPublished={Count}",
            correlationId, count);
        return count;
    }
}
