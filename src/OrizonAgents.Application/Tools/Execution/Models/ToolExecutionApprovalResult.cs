namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record ToolExecutionApprovalResult(bool Approved, Guid? ExecutionId)
{
    public static ToolExecutionApprovalResult Success(Guid executionId) => new(true, executionId);
    public static ToolExecutionApprovalResult NotAvailable() => new(false, null);
}
