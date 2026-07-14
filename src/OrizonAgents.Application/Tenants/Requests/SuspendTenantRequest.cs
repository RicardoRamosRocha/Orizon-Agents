namespace OrizonAgents.Application.Tenants.Requests;

public sealed record SuspendTenantRequest(
    Guid TenantId,
    string Reason,
    string ConcurrencyStamp);
