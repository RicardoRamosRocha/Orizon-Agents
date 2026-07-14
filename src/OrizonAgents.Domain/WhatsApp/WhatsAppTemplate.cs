using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.WhatsApp;

public sealed class WhatsAppTemplate : Entity, ITenantOwnedEntity
{
    private WhatsAppTemplate()
    {
        MetaTemplateId = string.Empty;
        Name = string.Empty;
        Language = string.Empty;
        Category = string.Empty;
        ComponentsJson = "{}";
    }

    private WhatsAppTemplate(Guid tenantId, Guid connectionId, string metaTemplateId, string name, string language, string category, WhatsAppTemplateStatus status, string componentsJson, DateTime utcNow)
        : this()
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (connectionId == Guid.Empty) throw new ArgumentException("Conexão é obrigatória.", nameof(connectionId));
        EnsureUtc(utcNow);
        TenantId = tenantId;
        WhatsAppConnectionId = connectionId;
        MetaTemplateId = Required(metaTemplateId, 120);
        Name = Required(name, 160);
        Language = Required(language, 16);
        Category = Required(category, 64);
        Status = status;
        ComponentsJson = string.IsNullOrWhiteSpace(componentsJson) ? "{}" : componentsJson;
        LastSynchronizedAtUtc = utcNow;
    }

    public Guid TenantId { get; private set; }

    public Guid WhatsAppConnectionId { get; private set; }

    public string MetaTemplateId { get; private set; }

    public string Name { get; private set; }

    public string Language { get; private set; }

    public string Category { get; private set; }

    public WhatsAppTemplateStatus Status { get; private set; }

    public string ComponentsJson { get; private set; }

    public DateTime LastSynchronizedAtUtc { get; private set; }

    public WhatsAppConnection Connection { get; private set; } = null!;

    public static WhatsAppTemplate Upsert(Guid tenantId, Guid connectionId, string metaTemplateId, string name, string language, string category, WhatsAppTemplateStatus status, string componentsJson, DateTime utcNow)
        => new(tenantId, connectionId, metaTemplateId, name, language, category, status, componentsJson, utcNow);

    public void Update(string name, string language, string category, WhatsAppTemplateStatus status, string componentsJson, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        Name = Required(name, 160);
        Language = Required(language, 16);
        Category = Required(category, 64);
        Status = status;
        ComponentsJson = string.IsNullOrWhiteSpace(componentsJson) ? "{}" : componentsJson;
        LastSynchronizedAtUtc = utcNow;
    }

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Valor obrigatório.", nameof(value));
        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Datas devem estar em UTC.", nameof(dateTime));
    }
}
