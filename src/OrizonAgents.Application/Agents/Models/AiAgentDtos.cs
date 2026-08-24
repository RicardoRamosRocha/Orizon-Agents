namespace OrizonAgents.Application.Agents.Models;

public sealed record AiAgentListItemDto(
    Guid Id,
    string Name,
    string? Description,
    string Provider,
    string Model,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record AiAgentDetailsDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    string SystemPrompt,
    string Provider,
    string Model,
    double Temperature,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
