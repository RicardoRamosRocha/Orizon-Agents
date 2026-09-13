namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record AgentToolExecutionResult(
    AgentToolExecutionStatus Status,
    int? StatusCode,
    string? Content,
    string? Error,
    Guid? ApprovalId = null)
{
    public bool Succeeded =>
        Status == AgentToolExecutionStatus.Succeeded;

    public bool RequiresApproval =>
        Status == AgentToolExecutionStatus.ApprovalRequired;

    public static AgentToolExecutionResult Success(
        int? statusCode,
        string? content)
    {
        return new AgentToolExecutionResult(
            AgentToolExecutionStatus.Succeeded,
            statusCode,
            content,
            null);
    }

    public static AgentToolExecutionResult Failure(
        string error,
        int? statusCode = null,
        string? content = null)
    {
        return new AgentToolExecutionResult(
            AgentToolExecutionStatus.Failed,
            statusCode,
            content,
            error);
    }

    public static AgentToolExecutionResult ApprovalRequired(
        Guid approvalId)
    {
        if (approvalId == Guid.Empty)
        {
            throw new ArgumentException(
                "ApprovalId é obrigatório.",
                nameof(approvalId));
        }

        return new AgentToolExecutionResult(
            AgentToolExecutionStatus.ApprovalRequired,
            null,
            null,
            null,
            approvalId);
    }

    public static AgentToolExecutionResult Rejected(
        Guid? approvalId = null)
    {
        return new AgentToolExecutionResult(
            AgentToolExecutionStatus.Rejected,
            null,
            null,
            null,
            approvalId);
    }
}
