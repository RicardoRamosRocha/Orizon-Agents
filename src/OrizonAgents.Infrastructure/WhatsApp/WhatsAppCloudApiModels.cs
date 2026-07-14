namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed record WhatsAppCloudNumber(string PhoneNumberId, string DisplayPhoneNumber, string VerifiedName, string QualityRating);

public sealed record WhatsAppCloudSendResult(bool Succeeded, bool IsTransient, string? ExternalMessageId, string? ErrorCode, string? ErrorMessage, TimeSpan? RetryAfter);

public sealed record WhatsAppCloudTemplate(string MetaTemplateId, string Name, string Language, string Category, string Status, string ComponentsJson);
