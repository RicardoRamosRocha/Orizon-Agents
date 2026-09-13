using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Application.Tools.Models;

public sealed record AgentToolDefinition(
    Guid Id,
    string Name,
    string Description,
    string HttpMethod,
    string? InputSchema,
    AgentToolRiskLevel RiskLevel,
    AgentToolKind Kind = AgentToolKind.Http);
