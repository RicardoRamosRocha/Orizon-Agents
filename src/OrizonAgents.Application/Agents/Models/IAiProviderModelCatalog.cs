using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Application.Agents.Models;

public sealed record AiProviderModel(
    string Id,
    string DisplayName);

public interface IAiProviderModelCatalog
{
    Task<IReadOnlyList<AiProviderModel>> ListAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default);

    Task<bool> IsValidAsync(
        AiProvider provider,
        string model,
        CancellationToken cancellationToken = default);
}
