namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record AgentToolExecutionResult(
    bool Succeeded,
    int? StatusCode,
    string? Content,
    string? Error)
{
    public static AgentToolExecutionResult Success(
        int? statusCode,
        string? content)
    {
        return new AgentToolExecutionResult(
            true,
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
            false,
            statusCode,
            content,
            error);
    }
}
