namespace OrizonAgents.Application.Tools.Requests;

public sealed record CreateAgentToolRequest(
    Guid TenantId,
    string Name,
    string Description,
    string Endpoint,
    string HttpMethod,
    string? InputSchema,
    Guid? ToolCredentialId = null);

public sealed record UpdateAgentToolRequest(
    Guid ToolId,
    string Name,
    string Description,
    string Endpoint,
    string HttpMethod,
    string? InputSchema,
    Guid? ToolCredentialId = null);
