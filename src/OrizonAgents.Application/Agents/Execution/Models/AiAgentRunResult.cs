namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiAgentRunResult(
    Guid ConversationId,
    string Response,
    AiAgentRunStatus Status = AiAgentRunStatus.Completed,
    Guid? ApprovalId = null)
{
    public bool RequiresApproval =>
        Status == AiAgentRunStatus.ApprovalRequired;
}
