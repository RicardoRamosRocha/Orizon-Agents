using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Integrations;

public sealed class ApiCredentialService : IApiCredentialService
{
    private const string KeyPrefix = "orizon_";
    private const char KeySeparator = '.';

    private readonly OrizonAgentsDbContext _dbContext;

    public ApiCredentialService(OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ApiCredentialListItem>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredId(tenantId, nameof(tenantId), "TenantId");

        return await _dbContext.ApiCredentials
            .AsNoTracking()
            .Where(credential =>
                credential.TenantId == tenantId &&
                credential.AgentId != null &&
                credential.KeyIdentifier != null)
            .OrderByDescending(credential => credential.CreatedAtUtc)
            .Select(credential => new ApiCredentialListItem(
                credential.Id,
                credential.AgentId!.Value,
                credential.Agent!.Name,
                credential.Name,
                credential.KeyIdentifier!,
                credential.IsActive,
                credential.CreatedAtUtc,
                credential.RevokedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreatedApiCredential> CreateAsync(
        Guid tenantId,
        Guid agentId,
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredId(tenantId, nameof(tenantId), "TenantId");
        ValidateRequiredId(agentId, nameof(agentId), "AgentId");

        bool agentExists = await _dbContext.AiAgents
            .AsNoTracking()
            .AnyAsync(
                agent =>
                    agent.Id == agentId &&
                    agent.TenantId == tenantId,
                cancellationToken);

        if (!agentExists)
        {
            throw new InvalidOperationException(
                "O agente informado não existe na organização.");
        }

        (ApiCredential credential, string apiKey) = CreateCredential(
            tenantId,
            agentId,
            name);
        _dbContext.ApiCredentials.Add(credential);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToCreatedCredential(credential, apiKey);
    }

    public Task<CreatedApiCredential> CreateAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Credenciais tenant-wide não são mais suportadas. Informe o agente.");
    }

    public async Task RevokeAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        ApiCredential credential = await FindAgentCredentialAsync(
            tenantId,
            credentialId,
            cancellationToken);

        credential.Revoke(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CreatedApiCredential> RegenerateAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        ApiCredential currentCredential = await FindAgentCredentialAsync(
            tenantId,
            credentialId,
            cancellationToken);

        if (!currentCredential.IsActive ||
            currentCredential.RevokedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "A credencial informada já está revogada.");
        }

        currentCredential.Revoke(DateTime.UtcNow);

        (ApiCredential replacement, string apiKey) = CreateCredential(
            currentCredential.TenantId,
            currentCredential.AgentId!.Value,
            currentCredential.Name);

        _dbContext.ApiCredentials.Add(replacement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToCreatedCredential(replacement, apiKey);
    }

    public async Task<ResolvedApiCredential?> ResolveAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        string normalizedApiKey = apiKey.Trim();
        if (!TryGetKeyIdentifier(normalizedApiKey, out string keyIdentifier))
        {
            return null;
        }

        string keyHash = ComputeHash(normalizedApiKey);

        ApiCredential? credential = await _dbContext.ApiCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.KeyIdentifier == keyIdentifier &&
                    candidate.KeyHash == keyHash &&
                    candidate.AgentId != null &&
                    candidate.IsActive &&
                    candidate.RevokedAtUtc == null,
                cancellationToken);

        if (credential is null)
        {
            return null;
        }

        bool tenantIsActive = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(
                tenant =>
                    tenant.Id == credential.TenantId &&
                    tenant.Status == Domain.Tenants.TenantStatus.Active,
                cancellationToken);

        if (!tenantIsActive)
        {
            return null;
        }

        return new ResolvedApiCredential(
            credential.Id,
            credential.TenantId,
            credential.AgentId!.Value,
            credential.KeyIdentifier!,
            credential.Name);
    }

    private async Task<ApiCredential> FindAgentCredentialAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        ValidateRequiredId(tenantId, nameof(tenantId), "TenantId");
        ValidateRequiredId(credentialId, nameof(credentialId), "CredentialId");

        ApiCredential? credential = await _dbContext.ApiCredentials
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == credentialId &&
                    candidate.TenantId == tenantId &&
                    candidate.AgentId != null,
                cancellationToken);

        return credential ?? throw new InvalidOperationException(
            "A credencial informada não existe para a organização.");
    }

    private static (ApiCredential Credential, string ApiKey) CreateCredential(
        Guid tenantId,
        Guid agentId,
        string name)
    {
        string keyIdentifier = GenerateRandomToken(12);
        string secret = GenerateRandomToken(32);
        string apiKey = $"{KeyPrefix}{keyIdentifier}{KeySeparator}{secret}";
        string keyHash = ComputeHash(apiKey);

        var credential = new ApiCredential(
            tenantId,
            agentId,
            name,
            keyIdentifier,
            keyHash);

        return (credential, apiKey);
    }

    private static CreatedApiCredential ToCreatedCredential(
        ApiCredential credential,
        string apiKey)
    {
        return new CreatedApiCredential(
            credential.Id,
            credential.TenantId,
            credential.AgentId!.Value,
            credential.Name,
            credential.KeyIdentifier!,
            apiKey);
    }

    private static string GenerateRandomToken(int byteCount)
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(byteCount);

        return Convert
            .ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string ComputeHash(string apiKey)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(apiKey);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static bool TryGetKeyIdentifier(
        string apiKey,
        out string keyIdentifier)
    {
        keyIdentifier = string.Empty;

        if (!apiKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        int separatorIndex = apiKey.IndexOf(
            KeySeparator,
            KeyPrefix.Length);

        if (separatorIndex <= KeyPrefix.Length ||
            separatorIndex == apiKey.Length - 1)
        {
            return false;
        }

        keyIdentifier = apiKey[KeyPrefix.Length..separatorIndex];
        return keyIdentifier.Length <= ApiCredential.KeyIdentifierMaxLength;
    }

    private static void ValidateRequiredId(
        Guid value,
        string parameterName,
        string displayName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                $"{displayName} é obrigatório.",
                parameterName);
        }
    }
}
