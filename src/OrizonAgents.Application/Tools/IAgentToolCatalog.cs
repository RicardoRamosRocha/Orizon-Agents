using OrizonAgents.Application.Tools.Models;

namespace OrizonAgents.Application.Tools;

public interface IAgentToolCatalog
{
    Task<IReadOnlyList<AgentToolDefinition>> GetAvailableToolsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}
