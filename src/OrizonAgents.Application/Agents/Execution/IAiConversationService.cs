using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.Application.Agents.Execution;

public interface IAiConversationService
{
    Task<AiConversationDto?> GetAsync(
        Guid conversationId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
