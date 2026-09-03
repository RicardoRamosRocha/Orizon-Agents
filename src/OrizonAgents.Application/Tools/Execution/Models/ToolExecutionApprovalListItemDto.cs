using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record ToolExecutionApprovalListItemDto(
    Guid Id,
    Guid AgentId,
    string AgentName,
    Guid ToolId,
    string ToolName,
    AgentToolRiskLevel RiskLevel,
    DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc);
