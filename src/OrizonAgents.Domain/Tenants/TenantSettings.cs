using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tenants;

public sealed class TenantSettings : AuditableEntity, ITenantOwnedEntity
{
    public const int CultureMaxLength = 16;
    public const int TimeZoneMaxLength = 64;
    public const int ContactNameMaxLength = 120;
    public const int ContactEmailMaxLength = 256;
    public const int ContactPhoneMaxLength = 32;
    public const string DefaultCulture = "pt-BR";
    public const string DefaultTimeZone = "America/Sao_Paulo";

    private TenantSettings()
    {
        Culture = string.Empty;
        TimeZone = string.Empty;
        Tenant = null!;
    }

    private TenantSettings(Guid tenantId, string culture, string timeZone)
    {
        TenantId = tenantId != Guid.Empty
            ? tenantId
            : throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        Culture = EnsureCulture(culture);
        TimeZone = EnsureTimeZone(timeZone);
    }

    public Guid TenantId { get; private set; }

    public string Culture { get; private set; }

    public string TimeZone { get; private set; }

    public string? ContactName { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? ContactPhone { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    public static TenantSettings Create(
        Guid tenantId,
        string culture = DefaultCulture,
        string timeZone = DefaultTimeZone)
    {
        return new TenantSettings(tenantId, culture, timeZone);
    }

    public void UpdateLocalization(string culture, string timeZone)
    {
        Culture = EnsureCulture(culture);
        TimeZone = EnsureTimeZone(timeZone);
    }

    public void UpdateContact(string? contactName, string? contactEmail, string? contactPhone)
    {
        ContactName = EnsureOptional(contactName, ContactNameMaxLength, nameof(contactName));
        ContactEmail = EnsureOptional(contactEmail, ContactEmailMaxLength, nameof(contactEmail));
        ContactPhone = EnsureOptional(contactPhone, ContactPhoneMaxLength, nameof(contactPhone));
    }

    private static string EnsureCulture(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        string trimmed = culture.Trim();
        return trimmed.Length <= CultureMaxLength
            ? trimmed
            : throw new ArgumentException($"Culture cannot exceed {CultureMaxLength} characters.", nameof(culture));
    }

    private static string EnsureTimeZone(string timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);

        string trimmed = timeZone.Trim();
        return trimmed.Length <= TimeZoneMaxLength
            ? trimmed
            : throw new ArgumentException($"Time zone cannot exceed {TimeZoneMaxLength} characters.", nameof(timeZone));
    }

    private static string? EnsureOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
    }
}
