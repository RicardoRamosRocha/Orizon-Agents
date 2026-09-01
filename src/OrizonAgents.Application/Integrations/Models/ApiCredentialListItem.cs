namespace OrizonAgents.Application.Integrations.Models;

public sealed record ApiCredentialListItem(
    Guid Id,
    Guid AgentId,
    string AgentName,
    string Name,
    string KeyIdentifier,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc);
