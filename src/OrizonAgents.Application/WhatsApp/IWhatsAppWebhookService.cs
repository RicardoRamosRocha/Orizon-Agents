using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;

namespace OrizonAgents.Application.WhatsApp;

public interface IWhatsAppWebhookService
{
    OperationResult<string> Verify(WhatsAppWebhookVerificationRequest request);

    Task<OperationResult<WhatsAppWebhookResult>> ReceiveAsync(WhatsAppWebhookPostRequest request, CancellationToken cancellationToken = default);
}
