namespace OrizonAgents.Application.Agents.Models;

public sealed record AiProviderModel(
    string Id,
    string DisplayName);

public interface IAiProviderModelCatalog
{
    Task<IReadOnlyList<AiProviderModel>> ListAsync(
        CancellationToken cancellationToken = default);
}
