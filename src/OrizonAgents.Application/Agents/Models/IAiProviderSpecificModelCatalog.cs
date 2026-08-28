using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Application.Agents.Models;

public interface IAiProviderSpecificModelCatalog
{
    AiProvider Provider { get; }

    Task<IReadOnlyList<AiProviderModel>> ListAsync(
        CancellationToken cancellationToken = default);
}
