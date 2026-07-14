using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;
using OrizonAgents.Domain.WhatsApp;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed class WhatsAppWebhookService : IWhatsAppWebhookService
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly WhatsAppOptions _options;

    public WhatsAppWebhookService(OrizonAgentsDbContext dbContext, IOptions<WhatsAppOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public OperationResult<string> Verify(WhatsAppWebhookVerificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.VerifyToken)) return OperationResult<string>.Failure("Webhook não configurado.");
        if (request.Mode == "subscribe" && request.VerifyToken == _options.VerifyToken && !string.IsNullOrWhiteSpace(request.Challenge))
        {
            return OperationResult<string>.Success(request.Challenge);
        }

        return OperationResult<string>.Failure("Verificação inválida.");
    }

    public async Task<OperationResult<WhatsAppWebhookResult>> ReceiveAsync(WhatsAppWebhookPostRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AppSecret)) return OperationResult<WhatsAppWebhookResult>.Failure("Webhook não configurado.");
        if (System.Text.Encoding.UTF8.GetByteCount(request.RawBody) > _options.MaxPayloadBytes) return OperationResult<WhatsAppWebhookResult>.Failure("Payload excede o limite permitido.");
        if (!WhatsAppSecurity.VerifySignature(request.RawBody, request.SignatureHeader, _options.AppSecret)) return OperationResult<WhatsAppWebhookResult>.Failure("Assinatura inválida.");

        ParsedWhatsAppWebhook parsed = WhatsAppWebhookParser.Parse(request.RawBody);
        WhatsAppConnection? connection = null;
        if (!string.IsNullOrWhiteSpace(parsed.PhoneNumberId))
        {
            connection = await _dbContext.WhatsAppConnections.IgnoreQueryFilters().SingleOrDefaultAsync(candidate => candidate.PhoneNumberId == parsed.PhoneNumberId, cancellationToken);
        }

        if (connection is null) return OperationResult<WhatsAppWebhookResult>.Success(new WhatsAppWebhookResult(true, false, parsed.EventId));
        if (await _dbContext.WhatsAppInboxEvents.IgnoreQueryFilters().AnyAsync(@event => @event.TenantId == connection.TenantId && @event.EventId == parsed.EventId, cancellationToken))
        {
            return OperationResult<WhatsAppWebhookResult>.Success(new WhatsAppWebhookResult(true, true, parsed.EventId));
        }

        DateTime utcNow = DateTime.UtcNow;
        connection.MarkWebhookReceived(utcNow);
        _dbContext.WhatsAppInboxEvents.Add(WhatsAppInboxEvent.Create(connection.TenantId, connection.Id, parsed.EventId, request.RawBody, utcNow));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<WhatsAppWebhookResult>.Success(new WhatsAppWebhookResult(true, false, parsed.EventId));
    }
}
