namespace OrizonAgents.Application.Integrations.Models;

public sealed record CreatedApiCredential(
    Guid Id,
    Guid TenantId,
    string Name,
    string ApiKey);
