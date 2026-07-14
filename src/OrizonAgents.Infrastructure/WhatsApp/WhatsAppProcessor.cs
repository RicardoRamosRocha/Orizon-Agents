using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Domain.WhatsApp;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed class WhatsAppProcessor : IWhatsAppProcessor
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IWhatsAppTokenProtector _tokenProtector;
    private readonly IWhatsAppCloudApiClient _client;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppProcessor> _logger;

    public WhatsAppProcessor(OrizonAgentsDbContext dbContext, IWhatsAppTokenProtector tokenProtector, IWhatsAppCloudApiClient client, IOptions<WhatsAppOptions> options, ILogger<WhatsAppProcessor> logger)
    {
        _dbContext = dbContext;
        _tokenProtector = tokenProtector;
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WhatsAppProcessorResult> ProcessInboxAsync(CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        WhatsAppInboxEvent[] events = await _dbContext.WhatsAppInboxEvents.IgnoreQueryFilters()
            .Where(@event => (@event.Status == WhatsAppQueueStatus.Pending || @event.Status == WhatsAppQueueStatus.Failed) && (@event.NextAttemptAtUtc == null || @event.NextAttemptAtUtc <= utcNow))
            .OrderBy(@event => @event.ReceivedAtUtc)
            .Take(_options.ProcessorBatchSize)
            .ToArrayAsync(cancellationToken);

        int processed = 0;
        int failed = 0;
        int dead = 0;
        foreach (WhatsAppInboxEvent inbox in events)
        {
            try
            {
                inbox.MarkProcessing();
                await _dbContext.SaveChangesAsync(cancellationToken);
                await ProcessInboxEventAsync(inbox, cancellationToken);
                inbox.MarkProcessed(DateTime.UtcNow);
                processed++;
            }
            catch (Exception exception)
            {
                inbox.MarkFailed(exception.Message, DateTime.UtcNow.AddMinutes(Math.Max(1, inbox.AttemptCount + 1)), Math.Max(1, _options.RetryCount));
                if (inbox.Status == WhatsAppQueueStatus.DeadLetter) dead++; else failed++;
                _logger.LogWarning("WhatsApp inbox event {EventId} failed with sanitized error.", inbox.EventId);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new WhatsAppProcessorResult(processed, failed, dead);
    }

    public async Task<WhatsAppProcessorResult> ProcessOutboxAsync(CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        WhatsAppOutboxMessage[] messages = await _dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters()
            .Where(message => (message.Status == WhatsAppQueueStatus.Pending || message.Status == WhatsAppQueueStatus.Failed) && (message.NextAttemptAtUtc == null || message.NextAttemptAtUtc <= utcNow))
            .OrderBy(message => message.CreatedAtUtc)
            .Take(_options.ProcessorBatchSize)
            .ToArrayAsync(cancellationToken);

        int processed = 0;
        int failed = 0;
        int dead = 0;
        foreach (WhatsAppOutboxMessage outbox in messages)
        {
            try
            {
                outbox.MarkProcessing();
                await _dbContext.SaveChangesAsync(cancellationToken);
                WhatsAppConnection connection = await _dbContext.WhatsAppConnections.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == outbox.WhatsAppConnectionId, cancellationToken);
                WhatsAppMessage message = await _dbContext.WhatsAppMessages.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == outbox.WhatsAppMessageId, cancellationToken);
                string token = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
                WhatsAppCloudSendResult result = await SendOutboxAsync(token, connection, message, cancellationToken);
                if (!result.Succeeded)
                {
                    message.ApplyStatus(WhatsAppMessageStatus.Failed, DateTime.UtcNow, result.ErrorCode, result.ErrorMessage);
                    outbox.MarkFailed(result.ErrorMessage ?? "Falha ao enviar mensagem.", DateTime.UtcNow.Add(result.RetryAfter ?? TimeSpan.FromMinutes(1)), Math.Max(1, _options.RetryCount), !result.IsTransient);
                    if (outbox.Status == WhatsAppQueueStatus.DeadLetter) dead++; else failed++;
                }
                else
                {
                    message.MarkAccepted(result.ExternalMessageId ?? Guid.NewGuid().ToString("N"), DateTime.UtcNow);
                    outbox.MarkProcessed(DateTime.UtcNow);
                    processed++;
                }
            }
            catch (Exception exception)
            {
                outbox.MarkFailed(exception.Message, DateTime.UtcNow.AddMinutes(Math.Max(1, outbox.AttemptCount + 1)), Math.Max(1, _options.RetryCount), false);
                if (outbox.Status == WhatsAppQueueStatus.DeadLetter) dead++; else failed++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new WhatsAppProcessorResult(processed, failed, dead);
    }

    public async Task<int> PruneInboxPayloadsAsync(CancellationToken cancellationToken = default)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, _options.InboxRetentionDays));
        WhatsAppInboxEvent[] oldEvents = await _dbContext.WhatsAppInboxEvents.IgnoreQueryFilters()
            .Where(@event => @event.ReceivedAtUtc <= cutoff && @event.PayloadJson != "{}")
            .Take(_options.ProcessorBatchSize)
            .ToArrayAsync(cancellationToken);
        foreach (WhatsAppInboxEvent oldEvent in oldEvents)
        {
            oldEvent.SanitizePayload(DateTime.UtcNow);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return oldEvents.Length;
    }

    private async Task ProcessInboxEventAsync(WhatsAppInboxEvent inbox, CancellationToken cancellationToken)
    {
        ParsedWhatsAppWebhook parsed = WhatsAppWebhookParser.Parse(inbox.PayloadJson);
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.IgnoreQueryFilters().SingleOrDefaultAsync(candidate => candidate.Id == inbox.WhatsAppConnectionId, cancellationToken);
        if (connection is null) return;
        foreach (ParsedIncomingMessage incoming in parsed.Messages)
        {
            if (await _dbContext.WhatsAppMessages.IgnoreQueryFilters().AnyAsync(message => message.TenantId == connection.TenantId && message.ExternalMessageId == incoming.ExternalMessageId, cancellationToken)) continue;
            _dbContext.WhatsAppMessages.Add(WhatsAppMessage.CreateIncoming(connection.TenantId, connection.Id, incoming.ExternalMessageId, incoming.Type, incoming.From, incoming.To, incoming.Text, incoming.MediaId, WhatsAppWebhookParser.FromUnix(incoming.TimestampUnix)));
        }

        foreach (ParsedStatusUpdate status in parsed.Statuses)
        {
            WhatsAppMessage? message = await _dbContext.WhatsAppMessages.IgnoreQueryFilters().SingleOrDefaultAsync(candidate => candidate.TenantId == connection.TenantId && candidate.ExternalMessageId == status.ExternalMessageId, cancellationToken);
            message?.ApplyStatus(status.Status, WhatsAppWebhookParser.FromUnix(status.TimestampUnix), status.ErrorCode, status.ErrorMessage);
        }
    }

    private Task<WhatsAppCloudSendResult> SendOutboxAsync(string token, WhatsAppConnection connection, WhatsAppMessage message, CancellationToken cancellationToken)
    {
        return message.Type switch
        {
            WhatsAppMessageType.Template => _client.SendTemplateAsync(token, connection.PhoneNumberId, message.Recipient, message.TemplateName ?? string.Empty, "pt_BR", cancellationToken),
            WhatsAppMessageType.Image or WhatsAppMessageType.Document or WhatsAppMessageType.Audio or WhatsAppMessageType.Video => _client.SendMediaAsync(token, connection.PhoneNumberId, message.Recipient, message.MediaId ?? string.Empty, message.Type.ToString().ToLowerInvariant(), message.TextContent ?? string.Empty, cancellationToken),
            _ => _client.SendTextAsync(token, connection.PhoneNumberId, message.Recipient, message.TextContent ?? string.Empty, cancellationToken)
        };
    }
}
