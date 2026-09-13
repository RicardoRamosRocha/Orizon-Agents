using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Application.Agents.Models;

public interface IAiProviderSpecificModelCatalog
{
    AiProvider Provider { get; }

    string DisplayName { get; }

    Task<IReadOnlyList<AiProviderModel>> ListAsync(
        CancellationToken cancellationToken = default);
}
