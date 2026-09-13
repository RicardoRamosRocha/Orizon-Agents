using OrizonAgents.Application.Integrations.Models;

namespace OrizonAgents.Application.Integrations;

public interface IApiCredentialService
{
    Task<IReadOnlyList<ApiCredentialListItem>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<CreatedApiCredential> CreateAsync(
        Guid tenantId,
        Guid agentId,
        string name,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    Task<CreatedApiCredential> RegenerateAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    Task<CreatedApiCredential> CreateAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken = default);

    Task<ResolvedApiCredential?> ResolveAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}
