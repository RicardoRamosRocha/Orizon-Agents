using Microsoft.AspNetCore.Identity;
using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int FullNameMaxLength = 160;

    public Guid? TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public Tenant? Tenant { get; set; }
}
