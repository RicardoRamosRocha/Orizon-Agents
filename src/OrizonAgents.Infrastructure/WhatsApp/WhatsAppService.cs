using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.WhatsApp.Models;
using OrizonAgents.Application.WhatsApp.Requests;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Domain.WhatsApp;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed class WhatsAppService : IWhatsAppConnectionService, IWhatsAppMessagingService, IWhatsAppTemplateService, IWhatsAppPlatformService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IEntitlementService _entitlementService;
    private readonly IWhatsAppTokenProtector _tokenProtector;
    private readonly IWhatsAppCloudApiClient _client;

    public WhatsAppService(
        OrizonAgentsDbContext dbContext,
        IEntitlementService entitlementService,
        IWhatsAppTokenProtector tokenProtector,
        IWhatsAppCloudApiClient client)
    {
        _dbContext = dbContext;
        _entitlementService = entitlementService;
        _tokenProtector = tokenProtector;
        _client = client;
    }

    public async Task<WhatsAppConnectionSummaryDto> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        WhatsAppConnectionDto[] connections = await _dbContext.WhatsAppConnections.AsNoTracking()
            .Where(connection => connection.TenantId == tenantId)
            .OrderByDescending(connection => connection.IsDefault)
            .ThenBy(connection => connection.Name)
            .Select(connection => ToConnectionDto(connection, _tokenProtector.Mask(connection.EncryptedAccessToken)))
            .ToArrayAsync(cancellationToken);

        return new WhatsAppConnectionSummaryDto(connections, await GetUsageAsync(tenantId, cancellationToken));
    }

    public async Task<WhatsAppConnectionDto?> GetConnectionAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WhatsAppConnections.AsNoTracking()
            .Where(connection => connection.TenantId == tenantId && connection.Id == connectionId)
            .Select(connection => ToConnectionDto(connection, _tokenProtector.Mask(connection.EncryptedAccessToken)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateConnectionAsync(CreateWhatsAppConnectionRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _entitlementService.HasAvailableCapacityAsync(request.TenantId, PlanFeatureKeys.WhatsAppNumbers, cancellationToken: cancellationToken))
        {
            return OperationResult<Guid>.Failure("Limite de números de WhatsApp do plano atingido.");
        }

        if (await _dbContext.WhatsAppConnections.IgnoreQueryFilters().AnyAsync(connection => connection.PhoneNumberId == request.PhoneNumberId, cancellationToken))
        {
            return OperationResult<Guid>.Failure("Este Phone Number ID já está conectado.");
        }

        try
        {
            var connection = WhatsAppConnection.Create(
                request.TenantId,
                request.Name,
                request.WhatsAppBusinessAccountId,
                request.PhoneNumberId,
                request.DisplayPhoneNumber,
                request.VerifiedName,
                _tokenProtector.Protect(request.AccessToken),
                request.IsDefault);

            if (request.IsDefault)
            {
                await ClearDefaultAsync(request.TenantId, cancellationToken);
            }

            _dbContext.WhatsAppConnections.Add(connection);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult<Guid>.Success(connection.Id);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException or DbUpdateException)
        {
            return OperationResult<Guid>.Failure(SafeError(exception));
        }
    }

    public async Task<OperationResult> UpdateConnectionAsync(Guid tenantId, Guid connectionId, UpdateWhatsAppConnectionRequest request, CancellationToken cancellationToken = default)
    {
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId, cancellationToken);
        if (connection is null) return OperationResult.Failure("Conexão não encontrada.");
        try
        {
            connection.EnsureConcurrencyStamp(request.ConcurrencyStamp);
            connection.Update(request.Name, request.DisplayPhoneNumber, request.VerifiedName);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(SafeError(exception));
        }
    }

    public async Task<OperationResult> ReplaceTokenAsync(Guid tenantId, Guid connectionId, ReplaceWhatsAppTokenRequest request, CancellationToken cancellationToken = default)
    {
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId, cancellationToken);
        if (connection is null) return OperationResult.Failure("Conexão não encontrada.");
        try
        {
            connection.EnsureConcurrencyStamp(request.ConcurrencyStamp);
            connection.ReplaceEncryptedToken(_tokenProtector.Protect(request.AccessToken));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(SafeError(exception));
        }
    }

    public async Task<OperationResult> ValidateConnectionAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId, cancellationToken);
        if (connection is null) return OperationResult.Failure("Conexão não encontrada.");
        try
        {
            string token = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
            WhatsAppCloudNumber number = await _client.GetPhoneNumberAsync(token, connection.PhoneNumberId, cancellationToken);
            connection.MarkValidated(DateTime.UtcNow, ParseQuality(number.QualityRating), number.VerifiedName);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch
        {
            connection.MarkInvalid(DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Failure("Não foi possível validar a conexão com a Meta.");
        }
    }

    public async Task<OperationResult> SetDefaultAsync(Guid tenantId, Guid connectionId, string concurrencyStamp, CancellationToken cancellationToken = default)
    {
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId, cancellationToken);
        if (connection is null) return OperationResult.Failure("Conexão não encontrada.");
        try
        {
            connection.EnsureConcurrencyStamp(concurrencyStamp);
            await ClearDefaultAsync(tenantId, cancellationToken);
            connection.SetDefault(true);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(SafeError(exception));
        }
    }

    public async Task<OperationResult> DisconnectAsync(Guid tenantId, Guid connectionId, string concurrencyStamp, CancellationToken cancellationToken = default)
    {
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId, cancellationToken);
        if (connection is null) return OperationResult.Failure("Conexão não encontrada.");
        try
        {
            connection.EnsureConcurrencyStamp(concurrencyStamp);
            connection.Disconnect();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(SafeError(exception));
        }
    }

    public Task<OperationResult<Guid>> QueueTextAsync(SendWhatsAppTextRequest request, CancellationToken cancellationToken = default)
        => QueueOutgoingAsync(request.TenantId, request.ConnectionId, request.Recipient, WhatsAppMessageType.Text, request.Text, null, null, request.IdempotencyKey, cancellationToken);

    public Task<OperationResult<Guid>> QueueTemplateAsync(SendWhatsAppTemplateRequest request, CancellationToken cancellationToken = default)
        => QueueOutgoingAsync(request.TenantId, request.ConnectionId, request.Recipient, WhatsAppMessageType.Template, null, request.TemplateName, null, request.IdempotencyKey, cancellationToken);

    public Task<OperationResult<Guid>> QueueMediaAsync(SendWhatsAppMediaRequest request, CancellationToken cancellationToken = default)
        => QueueOutgoingAsync(request.TenantId, request.ConnectionId, request.Recipient, ParseType(request.Type), request.Caption, null, request.MediaId, request.IdempotencyKey, cancellationToken);

    public async Task<WhatsAppPagedMessagesDto> ListMessagesAsync(WhatsAppMessageListRequest request, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, request.PageNumber);
        int size = Math.Clamp(request.PageSize, 5, 100);
        IQueryable<WhatsAppMessage> query = _dbContext.WhatsAppMessages.AsNoTracking().Include(message => message.Connection).Where(message => message.TenantId == request.TenantId);
        if (Enum.TryParse(request.Direction, true, out WhatsAppMessageDirection direction)) query = query.Where(message => message.Direction == direction);
        if (Enum.TryParse(request.Status, true, out WhatsAppMessageStatus status)) query = query.Where(message => message.Status == status);
        int total = await query.CountAsync(cancellationToken);
        WhatsAppMessageDto[] items = await query.OrderByDescending(message => message.CreatedAtUtc).Skip((page - 1) * size).Take(size)
            .Select(message => new WhatsAppMessageDto(
                message.Id,
                message.Connection.Name,
                message.ExternalMessageId,
                message.Direction.ToString(),
                message.Type.ToString(),
                message.Status.ToString(),
                MaskPhone(message.Sender),
                MaskPhone(message.Recipient),
                message.TextContent == null ? null : message.TextContent.Substring(0, Math.Min(80, message.TextContent.Length)),
                message.TemplateName,
                message.ErrorMessage,
                message.CreatedAtUtc,
                message.SentAtUtc,
                message.DeliveredAtUtc,
                message.ReadAtUtc,
                message.FailedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new WhatsAppPagedMessagesDto(new PagedResult<WhatsAppMessageDto>(items, page, size, total), await GetUsageAsync(request.TenantId, cancellationToken));
    }

    public async Task<PagedResult<WhatsAppTemplateDto>> ListTemplatesAsync(WhatsAppTemplateListRequest request, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, request.PageNumber);
        int size = Math.Clamp(request.PageSize, 5, 100);
        IQueryable<WhatsAppTemplate> query = _dbContext.WhatsAppTemplates.AsNoTracking().Include(template => template.Connection).Where(template => template.TenantId == request.TenantId);
        if (request.ConnectionId.HasValue) query = query.Where(template => template.WhatsAppConnectionId == request.ConnectionId.Value);
        if (Enum.TryParse(request.Status, true, out WhatsAppTemplateStatus status)) query = query.Where(template => template.Status == status);
        int total = await query.CountAsync(cancellationToken);
        WhatsAppTemplateDto[] items = await query.OrderBy(template => template.Name).Skip((page - 1) * size).Take(size)
            .Select(template => new WhatsAppTemplateDto(template.Id, template.WhatsAppConnectionId, template.Connection.Name, template.MetaTemplateId, template.Name, template.Language, template.Category, template.Status.ToString(), template.LastSynchronizedAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<WhatsAppTemplateDto>(items, page, size, total);
    }

    public async Task<OperationResult<int>> SynchronizeTemplatesAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId, cancellationToken);
        if (connection is null) return OperationResult<int>.Failure("Conexão não encontrada.");
        try
        {
            string token = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
            IReadOnlyCollection<WhatsAppCloudTemplate> cloudTemplates = await _client.GetTemplatesAsync(token, connection.WhatsAppBusinessAccountId, cancellationToken);
            DateTime utcNow = DateTime.UtcNow;
            foreach (WhatsAppCloudTemplate cloud in cloudTemplates)
            {
                WhatsAppTemplate? template = await _dbContext.WhatsAppTemplates.SingleOrDefaultAsync(candidate => candidate.WhatsAppConnectionId == connectionId && candidate.MetaTemplateId == cloud.MetaTemplateId, cancellationToken);
                WhatsAppTemplateStatus status = ParseTemplateStatus(cloud.Status);
                if (template is null)
                {
                    _dbContext.WhatsAppTemplates.Add(WhatsAppTemplate.Upsert(tenantId, connectionId, cloud.MetaTemplateId, cloud.Name, cloud.Language, cloud.Category, status, cloud.ComponentsJson, utcNow));
                }
                else
                {
                    template.Update(cloud.Name, cloud.Language, cloud.Category, status, cloud.ComponentsJson, utcNow);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult<int>.Success(cloudTemplates.Count);
        }
        catch
        {
            return OperationResult<int>.Failure("Não foi possível sincronizar templates com a Meta.");
        }
    }

    public async Task<WhatsAppPlatformOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        int totalConnections = await _dbContext.WhatsAppConnections.IgnoreQueryFilters().AsNoTracking().CountAsync(cancellationToken);
        int activeConnections = await _dbContext.WhatsAppConnections.IgnoreQueryFilters().AsNoTracking().CountAsync(connection => connection.Status == WhatsAppConnectionStatus.Active, cancellationToken);
        int pendingInbox = await _dbContext.WhatsAppInboxEvents.IgnoreQueryFilters().AsNoTracking().CountAsync(@event => @event.Status == WhatsAppQueueStatus.Pending || @event.Status == WhatsAppQueueStatus.Failed, cancellationToken);
        int deadLetters = await _dbContext.WhatsAppInboxEvents.IgnoreQueryFilters().AsNoTracking().CountAsync(@event => @event.Status == WhatsAppQueueStatus.DeadLetter, cancellationToken)
            + await _dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters().AsNoTracking().CountAsync(message => message.Status == WhatsAppQueueStatus.DeadLetter, cancellationToken);
        int failedMessages = await _dbContext.WhatsAppMessages.IgnoreQueryFilters().AsNoTracking().CountAsync(message => message.Status == WhatsAppMessageStatus.Failed, cancellationToken);
        int processedMessages = await _dbContext.WhatsAppMessages.IgnoreQueryFilters().AsNoTracking().CountAsync(cancellationToken);
        WhatsAppConnectionDto[] recent = await _dbContext.WhatsAppConnections.IgnoreQueryFilters().AsNoTracking()
            .OrderByDescending(connection => connection.CreatedAtUtc)
            .Take(10)
            .Select(connection => ToConnectionDto(connection, "••••••••"))
            .ToArrayAsync(cancellationToken);
        return new WhatsAppPlatformOverviewDto(totalConnections, activeConnections, totalConnections - activeConnections, pendingInbox, deadLetters, failedMessages, processedMessages, recent);
    }

    private async Task<OperationResult<Guid>> QueueOutgoingAsync(Guid tenantId, Guid connectionId, string recipient, WhatsAppMessageType type, string? text, string? templateName, string? mediaId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!await _entitlementService.HasAvailableCapacityAsync(tenantId, PlanFeatureKeys.MonthlyMessages, cancellationToken: cancellationToken))
        {
            return OperationResult<Guid>.Failure("Limite mensal de mensagens do plano atingido.");
        }

        if (await _dbContext.WhatsAppOutboxMessages.AnyAsync(message => message.TenantId == tenantId && message.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            Guid existing = await _dbContext.WhatsAppOutboxMessages.Where(message => message.TenantId == tenantId && message.IdempotencyKey == idempotencyKey).Select(message => message.WhatsAppMessageId).SingleAsync(cancellationToken);
            return OperationResult<Guid>.Success(existing);
        }

        WhatsAppConnection? connection = await _dbContext.WhatsAppConnections.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == connectionId && candidate.Status != WhatsAppConnectionStatus.Disconnected, cancellationToken);
        if (connection is null) return OperationResult<Guid>.Failure("Conexão não encontrada ou desconectada.");

        DateTime utcNow = DateTime.UtcNow;
        WhatsAppMessage message = WhatsAppMessage.CreateOutgoing(tenantId, connectionId, type, connection.DisplayPhoneNumber, recipient, text, templateName, mediaId, utcNow);
        var payload = new { type = type.ToString(), recipient, text, templateName, mediaId };
        WhatsAppOutboxMessage outbox = WhatsAppOutboxMessage.Create(tenantId, connectionId, message.Id, idempotencyKey, JsonSerializer.Serialize(payload, JsonOptions), utcNow);
        WhatsAppMonthlyUsage usage = await GetOrCreateUsageAsync(tenantId, utcNow, cancellationToken);
        usage.IncrementOutgoingAccepted();
        _dbContext.WhatsAppMessages.Add(message);
        _dbContext.WhatsAppOutboxMessages.Add(outbox);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Success(message.Id);
    }

    private async Task<WhatsAppMonthlyUsage> GetOrCreateUsageAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken)
    {
        WhatsAppMonthlyUsage? usage = await _dbContext.WhatsAppMonthlyUsage.SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Year == utcNow.Year && candidate.Month == utcNow.Month, cancellationToken);
        if (usage is not null) return usage;
        usage = WhatsAppMonthlyUsage.Create(tenantId, utcNow);
        _dbContext.WhatsAppMonthlyUsage.Add(usage);
        return usage;
    }

    private async Task<WhatsAppUsageDto> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var numbers = await _entitlementService.GetUsageAsync(tenantId, PlanFeatureKeys.WhatsAppNumbers, cancellationToken);
        var messages = await _entitlementService.GetUsageAsync(tenantId, PlanFeatureKeys.MonthlyMessages, cancellationToken);
        return new WhatsAppUsageDto(numbers.Used, numbers.LimitValue, messages.Used, messages.LimitValue, messages.IsUnlimited ? int.MaxValue : Math.Max(0, messages.Available ?? 0));
    }

    private async Task ClearDefaultAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        WhatsAppConnection[] defaults = await _dbContext.WhatsAppConnections.Where(connection => connection.TenantId == tenantId && connection.IsDefault).ToArrayAsync(cancellationToken);
        foreach (WhatsAppConnection current in defaults) current.SetDefault(false);
    }

    private static WhatsAppConnectionDto ToConnectionDto(WhatsAppConnection connection, string maskedToken)
        => new(connection.Id, connection.TenantId, connection.Name, connection.WhatsAppBusinessAccountId, connection.PhoneNumberId, MaskPhone(connection.DisplayPhoneNumber), connection.VerifiedName, connection.Status.ToString(), connection.QualityRating.ToString(), connection.IsDefault, connection.LastValidatedAtUtc, connection.LastWebhookAtUtc, maskedToken, connection.ConcurrencyStamp);

    private static WhatsAppQualityRating ParseQuality(string quality)
        => Enum.TryParse(quality, true, out WhatsAppQualityRating value) ? value : WhatsAppQualityRating.Unknown;

    private static WhatsAppTemplateStatus ParseTemplateStatus(string status)
        => Enum.TryParse(status, true, out WhatsAppTemplateStatus value) ? value : WhatsAppTemplateStatus.Unknown;

    private static WhatsAppMessageType ParseType(string type)
        => Enum.TryParse(type, true, out WhatsAppMessageType value) ? value : WhatsAppMessageType.Unknown;

    private static string MaskPhone(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4) return "••••";
        return $"••••{digits[^4..]}";
    }

    private static string SafeError(Exception exception)
        => exception.Message.Contains("token", StringComparison.OrdinalIgnoreCase) ? "Erro ao processar credencial protegida." : exception.Message;
}
