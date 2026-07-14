using OrizonAgents.Application.Common.Paging;

namespace OrizonAgents.Application.WhatsApp.Models;

public sealed record WhatsAppConnectionDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string WhatsAppBusinessAccountId,
    string PhoneNumberId,
    string DisplayPhoneNumber,
    string VerifiedName,
    string Status,
    string QualityRating,
    bool IsDefault,
    DateTime? LastValidatedAtUtc,
    DateTime? LastWebhookAtUtc,
    string MaskedToken,
    string ConcurrencyStamp);

public sealed record WhatsAppConnectionSummaryDto(
    IReadOnlyCollection<WhatsAppConnectionDto> Connections,
    WhatsAppUsageDto Usage);

public sealed record WhatsAppUsageDto(
    int ActiveNumbers,
    int? NumberLimit,
    int MonthlyMessagesUsed,
    int? MonthlyMessageLimit,
    int RemainingMonthlyMessages);

public sealed record WhatsAppTemplateDto(
    Guid Id,
    Guid ConnectionId,
    string ConnectionName,
    string MetaTemplateId,
    string Name,
    string Language,
    string Category,
    string Status,
    DateTime LastSynchronizedAtUtc);

public sealed record WhatsAppMessageDto(
    Guid Id,
    string ConnectionName,
    string? ExternalMessageId,
    string Direction,
    string Type,
    string Status,
    string Sender,
    string Recipient,
    string? TextPreview,
    string? TemplateName,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? ReadAtUtc,
    DateTime? FailedAtUtc);

public sealed record WhatsAppPlatformOverviewDto(
    int TotalConnections,
    int ActiveConnections,
    int InactiveConnections,
    int PendingInboxEvents,
    int DeadLetters,
    int FailedMessages,
    int ProcessedMessages,
    IReadOnlyCollection<WhatsAppConnectionDto> RecentConnections);

public sealed record WhatsAppWebhookResult(bool Accepted, bool Duplicate, string? EventId);

public sealed record WhatsAppProcessorResult(int Processed, int Failed, int DeadLetters);

public sealed record WhatsAppPagedMessagesDto(PagedResult<WhatsAppMessageDto> Messages, WhatsAppUsageDto Usage);
