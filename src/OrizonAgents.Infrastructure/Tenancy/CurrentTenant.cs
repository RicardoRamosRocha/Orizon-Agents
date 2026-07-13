using OrizonAgents.Application.Common.Tenancy;

namespace OrizonAgents.Infrastructure.Tenancy;

public sealed class CurrentTenant : ICurrentTenant, ITenantContextSetter
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public void SetTenantId(Guid tenantId)
    {
        TenantId = tenantId != Guid.Empty
            ? tenantId
            : throw new ArgumentException("Tenant id is required.", nameof(tenantId));
    }

    public void Clear()
    {
        TenantId = null;
    }
}
