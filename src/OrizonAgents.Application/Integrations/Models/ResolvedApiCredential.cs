namespace OrizonAgents.Application.Integrations.Models;

public sealed record ResolvedApiCredential(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    string KeyIdentifier,
    string Name);
