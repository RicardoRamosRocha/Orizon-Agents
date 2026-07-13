using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Tenancy;

public class CurrentTenantTests
{
    [Fact]
    public void CurrentTenant_StartsWithoutTenant()
    {
        var currentTenant = new CurrentTenant();

        Assert.False(currentTenant.HasTenant);
        Assert.Null(currentTenant.TenantId);
    }

    [Fact]
    public void SetTenantId_WithValidTenantId_MakesTenantAvailable()
    {
        ITenantContextSetter setter = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();

        setter.SetTenantId(tenantId);
        var currentTenant = (ICurrentTenant)setter;

        Assert.True(currentTenant.HasTenant);
        Assert.Equal(tenantId, currentTenant.TenantId);
    }

    [Fact]
    public void SetTenantId_WithEmptyTenantId_ThrowsArgumentException()
    {
        ITenantContextSetter setter = new CurrentTenant();

        Assert.Throws<ArgumentException>(() => setter.SetTenantId(Guid.Empty));
    }

    [Fact]
    public void Clear_RemovesTenant()
    {
        ITenantContextSetter setter = new CurrentTenant();
        setter.SetTenantId(Guid.NewGuid());

        setter.Clear();
        var currentTenant = (ICurrentTenant)setter;

        Assert.False(currentTenant.HasTenant);
        Assert.Null(currentTenant.TenantId);
    }
}
