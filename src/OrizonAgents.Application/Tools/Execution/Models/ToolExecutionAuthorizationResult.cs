namespace OrizonAgents.Application.Tools.Execution.Models;

public enum ToolExecutionAuthorizationStatus
{
    Allowed = 1,
    ApprovalRequired = 2,
    Rejected = 3
}

public sealed record ToolExecutionAuthorizationResult(
    ToolExecutionAuthorizationStatus Status,
    Guid? ApprovalId = null)
{
    public bool IsAllowed =>
        Status == ToolExecutionAuthorizationStatus.Allowed;

    public static ToolExecutionAuthorizationResult Allowed()
        => new(ToolExecutionAuthorizationStatus.Allowed);

    public static ToolExecutionAuthorizationResult ApprovalRequired(
        Guid approvalId)
        => new(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            approvalId);

    public static ToolExecutionAuthorizationResult Rejected(
        Guid? approvalId = null)
        => new(
            ToolExecutionAuthorizationStatus.Rejected,
            approvalId);
}
