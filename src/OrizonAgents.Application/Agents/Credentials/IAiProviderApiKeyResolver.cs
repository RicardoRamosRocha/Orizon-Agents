using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Application.Agents.Credentials;

public interface IAiProviderApiKeyResolver
{
    Task<string?> ResolveAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default);
}
