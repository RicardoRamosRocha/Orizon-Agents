using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Application.Tools.Execution;

public interface IAgentToolExecutor
{
    Task<AgentToolExecutionResult> ExecuteAsync(
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken = default);
}
