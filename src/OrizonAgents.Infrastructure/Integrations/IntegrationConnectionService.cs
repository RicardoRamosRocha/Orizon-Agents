using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Application.Integrations.Requests;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Integrations;

public sealed class IntegrationConnectionService(
    OrizonAgentsDbContext dbContext,
    ICurrentTenant currentTenant) : IIntegrationConnectionService
{
    private static readonly Expression<Func<IntegrationConnection, IntegrationConnectionDto>> Projection =
        connection => new IntegrationConnectionDto(
            connection.Id, connection.Name, connection.Provider, connection.Status,
            connection.IsActive, connection.CreatedAtUtc, connection.UpdatedAtUtc);

    public async Task<IReadOnlyList<IntegrationConnectionDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await TenantConnections().AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(Projection).ToArrayAsync(cancellationToken);

    public Task<IntegrationConnectionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        TenantConnections().AsNoTracking().Where(x => x.Id == id)
            .Select(Projection).SingleOrDefaultAsync(cancellationToken);

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateIntegrationConnectionRequest request, CancellationToken cancellationToken = default)
    {
        Guid tenantId = GetRequiredTenantId();
        try
        {
            var connection = new IntegrationConnection(tenantId, request.Name, request.Provider);
            dbContext.IntegrationConnections.Add(connection);
            await dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult<Guid>.Success(connection.Id);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Guid>.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> UpdateAsync(
        Guid id, UpdateIntegrationConnectionRequest request, CancellationToken cancellationToken = default)
    {
        IntegrationConnection? connection = await TenantConnections().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (connection is null)
        {
            return OperationResult.Failure("Conexão não encontrada.");
        }

        try
        {
            connection.Rename(request.Name);
            await dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        IntegrationConnection? connection = await TenantConnections().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (connection is null)
        {
            return OperationResult.Failure("Conexão não encontrada.");
        }

        if (active)
        {
            connection.Activate();
        }
        else
        {
            connection.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IntegrationConnection? connection = await TenantConnections().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (connection is null)
        {
            return OperationResult.Failure("Conexão não encontrada.");
        }

        dbContext.IntegrationConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    private IQueryable<IntegrationConnection> TenantConnections()
    {
        Guid tenantId = GetRequiredTenantId();
        return dbContext.IntegrationConnections.Where(x => x.TenantId == tenantId);
    }

    private Guid GetRequiredTenantId()
    {
        if (!currentTenant.HasTenant || currentTenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Nenhum tenant está disponível para a conexão.");
        }

        return tenantId;
    }
}