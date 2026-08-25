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

    private readonly OrizonAgentsDbContext _dbContext;

    public ApiCredentialService(OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreatedApiCredential> CreateAsync(
        Guid tenantId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId é obrigatório.",
                nameof(tenantId));
        }

        bool tenantExists = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(
                tenant => tenant.Id == tenantId,
                cancellationToken);

        if (!tenantExists)
        {
            throw new InvalidOperationException(
                "A organização informada não existe.");
        }

        string apiKey = GenerateApiKey();
        string keyHash = ComputeHash(apiKey);

        var credential = new ApiCredential(
            tenantId,
            name,
            keyHash);

        _dbContext.ApiCredentials.Add(credential);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatedApiCredential(
            credential.Id,
            credential.TenantId,
            credential.Name,
            apiKey);
    }

    public async Task<ResolvedApiCredential?> ResolveAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        string keyHash = ComputeHash(apiKey.Trim());

        ApiCredential? credential = await _dbContext.ApiCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.KeyHash == keyHash &&
                    candidate.IsActive,
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
            credential.Name);
    }

    private static string GenerateApiKey()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

        string token = Convert
            .ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        return KeyPrefix + token;
    }

    private static string ComputeHash(string apiKey)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(apiKey);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}
