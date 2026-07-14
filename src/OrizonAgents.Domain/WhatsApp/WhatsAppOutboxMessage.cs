using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.WhatsApp;

public sealed class WhatsAppOutboxMessage : Entity, ITenantOwnedEntity
{
    private WhatsAppOutboxMessage()
    {
        IdempotencyKey = string.Empty;
        PayloadJson = "{}";
        CreatedAtUtc = DateTime.UtcNow;
    }

    private WhatsAppOutboxMessage(Guid tenantId, Guid connectionId, Guid messageId, string idempotencyKey, string payloadJson, DateTime utcNow)
        : this()
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (connectionId == Guid.Empty) throw new ArgumentException("Conexão é obrigatória.", nameof(connectionId));
        if (messageId == Guid.Empty) throw new ArgumentException("Mensagem é obrigatória.", nameof(messageId));
        EnsureUtc(utcNow);
        TenantId = tenantId;
        WhatsAppConnectionId = connectionId;
        WhatsAppMessageId = messageId;
        IdempotencyKey = Required(idempotencyKey, 160);
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        CreatedAtUtc = utcNow;
        NextAttemptAtUtc = utcNow;
    }

    public Guid TenantId { get; private set; }

    public Guid WhatsAppConnectionId { get; private set; }

    public Guid WhatsAppMessageId { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string PayloadJson { get; private set; }

    public WhatsAppQueueStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? NextAttemptAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? ErrorMessage { get; private set; }

    public static WhatsAppOutboxMessage Create(Guid tenantId, Guid connectionId, Guid messageId, string idempotencyKey, string payloadJson, DateTime utcNow)
        => new(tenantId, connectionId, messageId, idempotencyKey, payloadJson, utcNow);

    public void MarkProcessing()
    {
        Status = WhatsAppQueueStatus.Processing;
    }

    public void MarkProcessed(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Status = WhatsAppQueueStatus.Processed;
        ProcessedAtUtc = utcNow;
        ErrorMessage = null;
    }

    public void MarkFailed(string error, DateTime nextAttemptAtUtc, int maxAttempts, bool permanent)
    {
        EnsureUtc(nextAttemptAtUtc);
        AttemptCount++;
        ErrorMessage = Sanitize(error);
        Status = permanent || AttemptCount >= maxAttempts ? WhatsAppQueueStatus.DeadLetter : WhatsAppQueueStatus.Failed;
        NextAttemptAtUtc = Status == WhatsAppQueueStatus.DeadLetter ? null : nextAttemptAtUtc;
    }

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Valor obrigatório.", nameof(value));
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static string Sanitize(string value)
    {
        string cleaned = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return cleaned.Length <= 512 ? cleaned : cleaned[..512];
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Datas devem estar em UTC.", nameof(dateTime));
    }
}
