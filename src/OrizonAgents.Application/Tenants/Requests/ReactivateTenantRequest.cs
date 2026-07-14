namespace OrizonAgents.Application.Tenants.Requests;

public sealed record ReactivateTenantRequest(
    Guid TenantId,
    string ConcurrencyStamp);
