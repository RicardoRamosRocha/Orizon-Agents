using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Agents.Execution;

public interface IAiAgentRunner
{
    Task<OperationResult<string>> RunAsync(
        Guid agentId,
        string userMessage,
        IReadOnlyList<AiChatMessage>? history = null,
        CancellationToken cancellationToken = default);
}
