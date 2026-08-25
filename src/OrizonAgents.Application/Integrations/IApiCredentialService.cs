using OrizonAgents.Application.Integrations.Models;

namespace OrizonAgents.Application.Integrations;

public interface IApiCredentialService
{
    Task<CreatedApiCredential> CreateAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken = default);

    Task<ResolvedApiCredential?> ResolveAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}
