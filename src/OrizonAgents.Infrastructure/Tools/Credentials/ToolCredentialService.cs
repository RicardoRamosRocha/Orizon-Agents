using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools.Credentials;

public sealed class ToolCredentialService : IToolCredentialService
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IToolCredentialProtector _protector;

    public ToolCredentialService(
        OrizonAgentsDbContext dbContext,
        ICurrentTenant currentTenant,
        IToolCredentialProtector protector)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _protector = protector;
    }

    public async Task<IReadOnlyList<ToolCredentialListItemDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetRequiredTenantId();
        return await _dbContext.ToolCredentials
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new ToolCredentialListItemDto(
                x.Id,
                x.Name,
                x.AuthenticationType,
                x.HeaderName,
                x.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateToolCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetRequiredTenantId();
        if (request.TenantId != tenantId)
        {
            return OperationResult<Guid>.Failure("Tenant inválido para a credencial.");
        }

        try
        {
            string protectedSecret = _protector.Protect(request.Secret);
            var credential = new ToolCredential(
                tenantId,
                request.Name,
                request.AuthenticationType,
                request.HeaderName,
                protectedSecret);

            _dbContext.ToolCredentials.Add(credential);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult<Guid>.Success(credential.Id);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Guid>.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> RotateSecretAsync(
        Guid credentialId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetRequiredTenantId();
        ToolCredential? credential = await _dbContext.ToolCredentials
            .SingleOrDefaultAsync(
                x => x.Id == credentialId && x.TenantId == tenantId,
                cancellationToken);

        if (credential is null)
        {
            return OperationResult.Failure("Credencial não encontrada.");
        }

        try
        {
            credential.ReplaceSecret(_protector.Protect(secret));
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> SetActiveAsync(
        Guid credentialId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetRequiredTenantId();
        ToolCredential? credential = await _dbContext.ToolCredentials
            .SingleOrDefaultAsync(
                x => x.Id == credentialId && x.TenantId == tenantId,
                cancellationToken);

        if (credential is null)
        {
            return OperationResult.Failure("Credencial não encontrada.");
        }

        if (active)
        {
            credential.Activate();
        }
        else
        {
            credential.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    public async Task<ResolvedToolCredential?> ResolveForExecutionAsync(
        Guid credentialId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (credentialId == Guid.Empty || tenantId != GetRequiredTenantId())
        {
            return null;
        }

        ToolCredential? credential = await _dbContext.ToolCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == credentialId &&
                     x.TenantId == tenantId &&
                     x.IsActive,
                cancellationToken);

        if (credential is null)
        {
            return null;
        }

        try
        {
            return new ResolvedToolCredential(
                credential.AuthenticationType,
                credential.HeaderName,
                _protector.Unprotect(credential.EncryptedSecret));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private Guid GetRequiredTenantId()
    {
        if (!_currentTenant.HasTenant ||
            !_currentTenant.TenantId.HasValue ||
            _currentTenant.TenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Nenhum tenant está disponível para a credencial de Tool.");
        }

        return _currentTenant.TenantId.Value;
    }
}
