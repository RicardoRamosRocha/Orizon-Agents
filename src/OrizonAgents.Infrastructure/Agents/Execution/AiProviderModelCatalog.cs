using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class AiProviderModelCatalog : IAiProviderModelCatalog
{
    private readonly IReadOnlyDictionary<
        AiProvider,
        IAiProviderSpecificModelCatalog> _catalogs;

    public AiProviderModelCatalog(
        IEnumerable<IAiProviderSpecificModelCatalog> catalogs)
    {
        _catalogs = catalogs.ToDictionary(
            catalog => catalog.Provider);
    }

    public async Task<IReadOnlyList<AiProviderModel>> ListAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (!_catalogs.TryGetValue(
                provider,
                out IAiProviderSpecificModelCatalog? catalog))
        {
            return Array.Empty<AiProviderModel>();
        }

        return await catalog.ListAsync(
            cancellationToken);
    }

    public async Task<bool> IsValidAsync(
        AiProvider provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model) ||
            !_catalogs.TryGetValue(
                provider,
                out IAiProviderSpecificModelCatalog? catalog))
        {
            return false;
        }

        IReadOnlyList<AiProviderModel> models =
            await catalog.ListAsync(
                cancellationToken);

        return models.Any(item =>
            string.Equals(
                item.Id,
                model.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }
}
