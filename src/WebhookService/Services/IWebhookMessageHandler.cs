using WebhookService.Models;

namespace WebhookService.Services;

public interface IWebhookMessageHandler
{
    /// <summary>
    /// Extracts order messages from webhook payload and publishes OrderReceivedEvent for each.
    /// Returns number of messages published.
    /// </summary>
    Task<int> HandlePayloadAsync(WhatsAppWebhookPayload payload, CancellationToken cancellationToken = default);
}
