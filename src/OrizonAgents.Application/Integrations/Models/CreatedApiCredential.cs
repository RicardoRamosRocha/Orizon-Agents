namespace OrizonAgents.Application.Integrations.Models;

public sealed record CreatedApiCredential(
    Guid Id,
    Guid TenantId,
    Guid AgentId,
    string Name,
    string KeyIdentifier,
    string ApiKey);
