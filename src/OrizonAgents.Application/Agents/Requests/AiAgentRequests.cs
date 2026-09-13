namespace OrizonAgents.Application.Agents.Requests;

public sealed record CreateAiAgentRequest(
    Guid TenantId,
    string Name,
    string? Description,
    string SystemPrompt,
    string Provider,
    string Model,
    double Temperature);

public sealed record UpdateAiAgentRequest(
    Guid AgentId,
    string Name,
    string? Description,
    string SystemPrompt,
    string Provider,
    string Model,
    double Temperature);
