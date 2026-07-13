using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tenants;

public sealed class Tenant : AuditableEntity
{
    public const int NameMaxLength = 150;

    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
        Settings = null!;
    }

    private Tenant(string name, string slug)
    {
        Name = EnsureName(name);
        Slug = TenantSlug.Create(slug);
        Status = TenantStatus.Active;
        Settings = TenantSettings.Create(Id);
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public TenantStatus Status { get; private set; }

    public TenantSettings Settings { get; private set; }

    public static Tenant Create(string name, string? slug = null)
    {
        return new Tenant(name, slug ?? name);
    }

    public void Rename(string name)
    {
        Name = EnsureName(name);
    }

    public void ChangeSlug(string slug)
    {
        Slug = TenantSlug.Create(slug);
    }

    public void ChangeStatus(TenantStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Tenant status is invalid.");
        }

        Status = status;
    }

    private static string EnsureName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string trimmed = name.Trim();
        return trimmed.Length <= NameMaxLength
            ? trimmed
            : throw new ArgumentException($"Tenant name cannot exceed {NameMaxLength} characters.", nameof(name));
    }
}
