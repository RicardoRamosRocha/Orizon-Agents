using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.WhatsApp;

public sealed class WhatsAppMessage : Entity, ITenantOwnedEntity
{
    public const int ExternalMessageIdMaxLength = 160;
    public const int PhoneMaxLength = 32;
    public const int TextMaxLength = 4096;
    public const int ErrorCodeMaxLength = 64;
    public const int ErrorMessageMaxLength = 512;

    private WhatsAppMessage()
    {
        Sender = string.Empty;
        Recipient = string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private WhatsAppMessage(Guid tenantId, Guid connectionId, WhatsAppMessageDirection direction, WhatsAppMessageType type, WhatsAppMessageStatus status, string sender, string recipient, DateTime utcNow)
        : this()
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (connectionId == Guid.Empty) throw new ArgumentException("Conexão é obrigatória.", nameof(connectionId));
        EnsureUtc(utcNow);
        TenantId = tenantId;
        WhatsAppConnectionId = connectionId;
        Direction = direction;
        Type = type;
        Status = status;
        Sender = Trim(sender, PhoneMaxLength);
        Recipient = Trim(recipient, PhoneMaxLength);
        CreatedAtUtc = utcNow;
    }

    public Guid TenantId { get; private set; }

    public Guid WhatsAppConnectionId { get; private set; }

    public string? ExternalMessageId { get; private set; }

    public WhatsAppMessageDirection Direction { get; private set; }

    public WhatsAppMessageType Type { get; private set; }

    public WhatsAppMessageStatus Status { get; private set; }

    public string Sender { get; private set; }

    public string Recipient { get; private set; }

    public string? TextContent { get; private set; }

    public string? MediaId { get; private set; }

    public string? TemplateName { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public DateTime? FailedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public WhatsAppConnection Connection { get; private set; } = null!;

    public static WhatsAppMessage CreateIncoming(Guid tenantId, Guid connectionId, string externalMessageId, WhatsAppMessageType type, string sender, string recipient, string? text, string? mediaId, DateTime utcNow)
    {
        var message = new WhatsAppMessage(tenantId, connectionId, WhatsAppMessageDirection.Incoming, type, WhatsAppMessageStatus.Received, sender, recipient, utcNow);
        message.ExternalMessageId = Trim(externalMessageId, ExternalMessageIdMaxLength);
        message.TextContent = TrimOptional(text, TextMaxLength);
        message.MediaId = TrimOptional(mediaId, ExternalMessageIdMaxLength);
        return message;
    }

    public static WhatsAppMessage CreateOutgoing(Guid tenantId, Guid connectionId, WhatsAppMessageType type, string sender, string recipient, string? text, string? templateName, string? mediaId, DateTime utcNow)
    {
        var message = new WhatsAppMessage(tenantId, connectionId, WhatsAppMessageDirection.Outgoing, type, WhatsAppMessageStatus.Queued, sender, recipient, utcNow);
        message.TextContent = TrimOptional(text, TextMaxLength);
        message.TemplateName = TrimOptional(templateName, 160);
        message.MediaId = TrimOptional(mediaId, ExternalMessageIdMaxLength);
        return message;
    }

    public void MarkAccepted(string externalMessageId, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        ExternalMessageId = Trim(externalMessageId, ExternalMessageIdMaxLength);
        Status = WhatsAppMessageStatus.Sent;
        SentAtUtc = utcNow;
    }

    public void ApplyStatus(WhatsAppMessageStatus status, DateTime utcNow, string? errorCode = null, string? errorMessage = null)
    {
        EnsureUtc(utcNow);
        Status = status;
        if (status == WhatsAppMessageStatus.Sent) SentAtUtc = utcNow;
        if (status == WhatsAppMessageStatus.Delivered) DeliveredAtUtc = utcNow;
        if (status == WhatsAppMessageStatus.Read) ReadAtUtc = utcNow;
        if (status == WhatsAppMessageStatus.Failed)
        {
            FailedAtUtc = utcNow;
            ErrorCode = TrimOptional(errorCode, ErrorCodeMaxLength);
            ErrorMessage = SanitizeError(errorMessage);
        }
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Valor obrigatório.", nameof(value));
        string trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return trimmed;
    }

    private static string? TrimOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? SanitizeError(string? value)
    {
        string? trimmed = TrimOptional(value, ErrorMessageMaxLength);
        return trimmed?.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Datas devem estar em UTC.", nameof(dateTime));
    }
}
