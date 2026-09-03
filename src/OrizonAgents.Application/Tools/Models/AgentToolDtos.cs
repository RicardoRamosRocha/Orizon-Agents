namespace OrizonAgents.Application.Tools.Models;

public sealed record AgentToolListItemDto(
    Guid Id,
    string Name,
    string Description,
    string HttpMethod,
    string Endpoint,
    bool IsActive,
    string? CredentialName = null);

public sealed record AgentToolDetailsDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Description,
    string Endpoint,
    string HttpMethod,
    string? InputSchema,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    Guid? ToolCredentialId = null);

public sealed record AgentToolBindingDto(
    Guid ToolId,
    string Name,
    string Description,
    string HttpMethod,
    bool IsBound,
    bool IsActive);
