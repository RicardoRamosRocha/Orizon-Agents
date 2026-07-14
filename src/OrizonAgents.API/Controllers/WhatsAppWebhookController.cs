using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.WhatsApp.Requests;
using OrizonAgents.Infrastructure.WhatsApp;

namespace OrizonAgents.API.Controllers;

[ApiController]
[Route("api/webhooks/whatsapp")]
public sealed class WhatsAppWebhookController : ControllerBase
{
    private readonly IWhatsAppWebhookService _webhookService;
    private readonly WhatsAppOptions _options;

    public WhatsAppWebhookController(IWhatsAppWebhookService webhookService, IOptions<WhatsAppOptions> options)
    {
        _webhookService = webhookService;
        _options = options.Value;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var result = _webhookService.Verify(new WhatsAppWebhookVerificationRequest(mode, verifyToken, challenge));
        return result.Succeeded ? Content(result.Value ?? string.Empty, "text/plain") : Forbid();
    }

    [HttpPost]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (Request.ContentLength.HasValue && Request.ContentLength.Value > _options.MaxPayloadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var reader = new StreamReader(Request.Body, leaveOpen: false);
        string rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (System.Text.Encoding.UTF8.GetByteCount(rawBody) > _options.MaxPayloadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        string? signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var result = await _webhookService.ReceiveAsync(new WhatsAppWebhookPostRequest(rawBody, signature), cancellationToken);
        return result.Succeeded ? Ok(new { received = true }) : Unauthorized();
    }
}
