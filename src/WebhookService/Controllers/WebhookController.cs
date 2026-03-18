using Microsoft.AspNetCore.Mvc;
using WebhookService.Models;
using WebhookService.Services;

namespace WebhookService.Controllers;

[ApiController]
[Route("webhook")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookMessageHandler _handler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IWebhookMessageHandler handler,
        IConfiguration configuration,
        ILogger<WebhookController> logger)
    {
        _handler = handler;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// WhatsApp webhook verification: GET with hub.mode, hub.verify_token, hub.challenge.
    /// Returns hub.challenge if verify_token matches config.
    /// </summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery] string? hub_mode,
        [FromQuery] string? hub_verify_token,
        [FromQuery] string? hub_challenge)
    {
        var expectedToken = _configuration["Webhook:VerifyToken"] ?? "my-verify-token";
        if (hub_mode == "subscribe" && hub_verify_token == expectedToken)
        {
            _logger.LogInformation("Webhook verified with challenge {Challenge}", hub_challenge);
            return Ok(hub_challenge);
        }
        return Unauthorized();
    }

    /// <summary>
    /// Receives WhatsApp webhook POST with message payload. Publishes to queue and returns 200 quickly.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] WhatsAppWebhookPayload? payload, CancellationToken cancellationToken)
    {
        if (payload == null)
        {
            _logger.LogWarning("Webhook POST received with null body");
            return Ok();
        }

        try
        {
            var count = await _handler.HandlePayloadAsync(payload, cancellationToken);
            _logger.LogInformation("Processed webhook, published {Count} order message(s)", count);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return Ok();
        }
    }
}
