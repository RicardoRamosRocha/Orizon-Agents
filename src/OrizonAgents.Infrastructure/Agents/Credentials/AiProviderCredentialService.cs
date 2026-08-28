using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Agents.Credentials;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Agents.Credentials;

public sealed class AiProviderCredentialService
    : IAiProviderCredentialService
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAiProviderCredentialProtector _protector;

    public AiProviderCredentialService(
        OrizonAgentsDbContext dbContext,
        ICurrentTenant currentTenant,
        IAiProviderCredentialProtector protector)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _protector = protector;
    }

    public async Task SaveAsync(
        AiProvider provider,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetRequiredTenantId();

        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        string encryptedApiKey =
            _protector.Protect(apiKey.Trim());

        AiProviderCredential? credential =
            await _dbContext.AiProviderCredentials
                .SingleOrDefaultAsync(
                    x => x.Provider == provider,
                    cancellationToken);

        if (credential is null)
        {
            credential = new AiProviderCredential(
                tenantId,
                provider,
                encryptedApiKey);

            _dbContext.AiProviderCredentials.Add(credential);
        }
        else
        {
            credential.ReplaceApiKey(encryptedApiKey);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> ResolveAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        GetRequiredTenantId();

        AiProviderCredential? credential =
            await _dbContext.AiProviderCredentials
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Provider == provider &&
                        x.IsActive,
                    cancellationToken);

        if (credential is null)
        {
            return null;
        }

        return _protector.Unprotect(
            credential.EncryptedApiKey);
    }

    public async Task<bool> HasCredentialAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        GetRequiredTenantId();

        return await _dbContext.AiProviderCredentials
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Provider == provider &&
                    x.IsActive,
                cancellationToken);
    }

    public async Task RemoveAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        GetRequiredTenantId();

        AiProviderCredential? credential =
            await _dbContext.AiProviderCredentials
                .SingleOrDefaultAsync(
                    x => x.Provider == provider,
                    cancellationToken);

        if (credential is null)
        {
            return;
        }

        credential.Deactivate();

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Guid GetRequiredTenantId()
    {
        if (!_currentTenant.HasTenant ||
            !_currentTenant.TenantId.HasValue ||
            _currentTenant.TenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Nenhum tenant está disponível para resolver a credencial de IA.");
        }

        return _currentTenant.TenantId.Value;
    }
}
