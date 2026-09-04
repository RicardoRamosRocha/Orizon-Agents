using OrizonAgents.Domain.Integrations;

namespace OrizonAgents.Application.Integrations.Models;

public sealed record IntegrationConnectionDto(
    Guid Id,
    string Name,
    IntegrationProvider Provider,
    IntegrationConnectionStatus Status,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? ConnectedAccountEmail = null);