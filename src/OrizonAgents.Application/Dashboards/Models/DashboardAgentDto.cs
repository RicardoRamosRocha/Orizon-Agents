namespace OrizonAgents.Application.Dashboards.Models;

public sealed record DashboardAgentDto(
    Guid Id,
    string Name,
    string Provider,
    string Model,
    bool IsActive,
    int KnowledgeBaseCount,
    int ToolCount);
