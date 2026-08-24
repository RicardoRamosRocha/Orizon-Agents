using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Agents.Execution;

public interface IAiAgentRunner
{
    Task<OperationResult<AiAgentRunResult>> RunAsync(
        Guid agentId,
        string userMessage,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default);
}
