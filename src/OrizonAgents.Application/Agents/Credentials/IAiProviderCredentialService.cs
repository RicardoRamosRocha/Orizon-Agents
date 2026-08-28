using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Application.Agents.Credentials;

public interface IAiProviderCredentialService
{
    Task SaveAsync(
        AiProvider provider,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<string?> ResolveAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default);

    Task<bool> HasCredentialAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default);
}
