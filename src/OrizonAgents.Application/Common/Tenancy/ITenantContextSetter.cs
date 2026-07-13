namespace OrizonAgents.Application.Common.Tenancy;

public interface ITenantContextSetter
{
    void SetTenantId(Guid tenantId);

    void Clear();
}
