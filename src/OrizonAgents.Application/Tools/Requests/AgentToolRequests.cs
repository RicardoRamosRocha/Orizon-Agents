namespace OrizonAgents.Application.Tools.Requests;

public sealed record CreateAgentToolRequest(
    Guid TenantId,
    string Name,
    string Description,
    string Endpoint,
    string HttpMethod,
    string? InputSchema);

public sealed record UpdateAgentToolRequest(
    Guid ToolId,
    string Name,
    string Description,
    string Endpoint,
    string HttpMethod,
    string? InputSchema);
