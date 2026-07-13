namespace OrizonAgents.Application.Common.Tenancy;

public interface ICurrentTenant
{
    Guid? TenantId { get; }

    bool HasTenant { get; }
}
