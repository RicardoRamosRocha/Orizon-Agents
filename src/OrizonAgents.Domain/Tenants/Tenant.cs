using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tenants;

public sealed class Tenant : AuditableEntity
{
    public const int NameMaxLength = 150;
    public const int SuspensionReasonMaxLength = 500;

    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
        Settings = null!;
        ConcurrencyStamp = string.Empty;
    }

    private Tenant(string name, string slug)
    {
        Name = EnsureName(name);
        Slug = TenantSlug.Create(slug);
        Status = TenantStatus.Active;
        Settings = TenantSettings.Create(Id);
        ConcurrencyStamp = NewConcurrencyStamp();
    }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public TenantStatus Status { get; private set; }

    public string? SuspensionReason { get; private set; }

    public DateTime? SuspendedAtUtc { get; private set; }

    public string ConcurrencyStamp { get; private set; }

    public TenantSettings Settings { get; private set; }

    public static Tenant Create(string name, string? slug = null)
    {
        return new Tenant(name, slug ?? name);
    }

    public void Rename(string name)
    {
        Name = EnsureName(name);
        TouchConcurrency();
    }

    public void ChangeSlug(string slug)
    {
        Slug = TenantSlug.Create(slug);
        TouchConcurrency();
    }

    public void ChangeStatus(TenantStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Tenant status is invalid.");
        }

        Status = status;
        TouchConcurrency();
    }

    public void Suspend(string reason, DateTime utcNow)
    {
        EnsureUtc(utcNow);
        string trimmedReason = EnsureSuspensionReason(reason);

        Status = TenantStatus.Suspended;
        SuspensionReason = trimmedReason;
        SuspendedAtUtc = utcNow;
        TouchConcurrency();
    }

    public void Reactivate(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        Status = TenantStatus.Active;
        SuspensionReason = null;
        SuspendedAtUtc = null;
        TouchConcurrency();
    }

    public void EnsureConcurrencyStamp(string concurrencyStamp)
    {
        if (!string.Equals(ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A organização foi alterada por outro usuário. Recarregue a página e tente novamente.");
        }
    }

    private static string EnsureName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string trimmed = name.Trim();
        return trimmed.Length <= NameMaxLength
            ? trimmed
            : throw new ArgumentException($"Tenant name cannot exceed {NameMaxLength} characters.", nameof(name));
    }

    private static string EnsureSuspensionReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        string trimmed = reason.Trim();
        return trimmed.Length <= SuspensionReasonMaxLength
            ? trimmed
            : throw new ArgumentException($"Suspension reason cannot exceed {SuspensionReasonMaxLength} characters.", nameof(reason));
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Dates must be provided in UTC.", nameof(dateTime));
        }
    }

    private void TouchConcurrency()
    {
        ConcurrencyStamp = NewConcurrencyStamp();
    }

    private static string NewConcurrencyStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
