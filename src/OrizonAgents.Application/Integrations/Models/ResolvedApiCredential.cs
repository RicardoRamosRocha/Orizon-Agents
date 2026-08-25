namespace OrizonAgents.Application.Integrations.Models;

public sealed record ResolvedApiCredential(
    Guid Id,
    Guid TenantId,
    string Name);
