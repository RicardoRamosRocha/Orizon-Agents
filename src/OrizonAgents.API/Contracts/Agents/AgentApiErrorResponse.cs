namespace OrizonAgents.API.Contracts.Agents;

public sealed record AgentApiErrorResponse(
    bool Success,
    AgentApiError Error);

public sealed record AgentApiError(
    string Code,
    string Message);
