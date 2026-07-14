using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.WhatsApp;

public sealed class WhatsAppMedia : Entity, ITenantOwnedEntity
{
    private WhatsAppMedia()
    {
        MetaMediaId = string.Empty;
        MimeType = string.Empty;
        FileName = string.Empty;
        Sha256 = string.Empty;
    }

    private WhatsAppMedia(Guid tenantId, Guid connectionId, string metaMediaId, string mimeType, string fileName, long size, string sha256, DateTime utcNow, DateTime? expiresAtUtc)
        : this()
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (connectionId == Guid.Empty) throw new ArgumentException("Conexão é obrigatória.", nameof(connectionId));
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
        EnsureUtc(utcNow);
        if (expiresAtUtc.HasValue) EnsureUtc(expiresAtUtc.Value);
        TenantId = tenantId;
        WhatsAppConnectionId = connectionId;
        MetaMediaId = Required(metaMediaId, 160);
        MimeType = Required(mimeType, 120);
        FileName = Optional(fileName, 260);
        Size = size;
        Sha256 = Optional(sha256, 128);
        CreatedAtUtc = utcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid TenantId { get; private set; }

    public Guid WhatsAppConnectionId { get; private set; }

    public string MetaMediaId { get; private set; }

    public string MimeType { get; private set; }

    public string FileName { get; private set; }

    public long Size { get; private set; }

    public string Sha256 { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public WhatsAppConnection Connection { get; private set; } = null!;

    public static WhatsAppMedia Create(Guid tenantId, Guid connectionId, string metaMediaId, string mimeType, string fileName, long size, string sha256, DateTime utcNow, DateTime? expiresAtUtc)
        => new(tenantId, connectionId, metaMediaId, mimeType, fileName, size, sha256, utcNow, expiresAtUtc);

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Valor obrigatório.", nameof(value));
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static string Optional(string? value, int maxLength)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Datas devem estar em UTC.", nameof(dateTime));
    }
}
