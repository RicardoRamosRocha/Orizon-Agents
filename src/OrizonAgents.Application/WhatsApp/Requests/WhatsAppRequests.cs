using OrizonAgents.Application.Common.Paging;

namespace OrizonAgents.Application.WhatsApp.Requests;

public sealed record CreateWhatsAppConnectionRequest(
    Guid TenantId,
    string Name,
    string WhatsAppBusinessAccountId,
    string PhoneNumberId,
    string DisplayPhoneNumber,
    string VerifiedName,
    string AccessToken,
    bool IsDefault);

public sealed record UpdateWhatsAppConnectionRequest(
    string Name,
    string DisplayPhoneNumber,
    string VerifiedName,
    string ConcurrencyStamp);

public sealed record ReplaceWhatsAppTokenRequest(string AccessToken, string ConcurrencyStamp);

public sealed record SendWhatsAppTextRequest(Guid TenantId, Guid ConnectionId, string Recipient, string Text, string IdempotencyKey);

public sealed record SendWhatsAppTemplateRequest(Guid TenantId, Guid ConnectionId, string Recipient, string TemplateName, string Language, string IdempotencyKey);

public sealed record SendWhatsAppMediaRequest(Guid TenantId, Guid ConnectionId, string Recipient, string MediaId, string Caption, string Type, string IdempotencyKey);

public sealed record WhatsAppMessageListRequest(Guid TenantId, string? Direction, string? Status, int PageNumber = 1, int PageSize = 20);

public sealed record WhatsAppTemplateListRequest(Guid TenantId, Guid? ConnectionId, string? Status, int PageNumber = 1, int PageSize = 20);

public sealed record WhatsAppWebhookPostRequest(string RawBody, string? SignatureHeader);

public sealed record WhatsAppWebhookVerificationRequest(string? Mode, string? VerifyToken, string? Challenge);
