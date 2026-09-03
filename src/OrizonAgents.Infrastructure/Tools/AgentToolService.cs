using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools;

public sealed class AgentToolService : IAgentToolService
{
    private readonly OrizonAgentsDbContext _dbContext;

    public AgentToolService(OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AgentToolListItemDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AgentTools
            .AsNoTracking()
            .OrderBy(tool => tool.Name)
            .Select(tool => new AgentToolListItemDto(
                tool.Id,
                tool.Name,
                tool.Description,
                tool.HttpMethod,
                tool.Endpoint,
                tool.IsActive,
                tool.ToolCredential != null ? tool.ToolCredential.Name : null))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AgentToolDetailsDto?> GetAsync(
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AgentTools
            .AsNoTracking()
            .Where(tool => tool.Id == toolId)
            .Select(tool => new AgentToolDetailsDto(
                tool.Id,
                tool.TenantId,
                tool.Name,
                tool.Description,
                tool.Endpoint,
                tool.HttpMethod,
                tool.InputSchema,
                tool.IsActive,
                tool.CreatedAtUtc,
                tool.UpdatedAtUtc,
                tool.ToolCredentialId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateAgentToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            return OperationResult<Guid>.Failure(
                "Tenant é obrigatório.");
        }

        if (request.ToolCredentialId.HasValue &&
            !await _dbContext.ToolCredentials.AnyAsync(
                credential =>
                    credential.Id == request.ToolCredentialId.Value &&
                    credential.TenantId == request.TenantId,
                cancellationToken))
        {
            return OperationResult<Guid>.Failure(
                "Credencial de Tool não encontrada para o tenant.");
        }

        try
        {
            var tool = new AgentTool(
                request.TenantId,
                request.Name,
                request.Description,
                request.Endpoint,
                request.HttpMethod);

            tool.Update(
                request.Name,
                request.Description,
                request.Endpoint,
                request.HttpMethod,
                request.InputSchema,
                request.ToolCredentialId);

            _dbContext.AgentTools.Add(tool);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<Guid>.Success(tool.Id);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Guid>.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> UpdateAsync(
        UpdateAgentToolRequest request,
        CancellationToken cancellationToken = default)
    {
        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.ToolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        if (request.ToolCredentialId.HasValue &&
            !await _dbContext.ToolCredentials.AnyAsync(
                credential =>
                    credential.Id == request.ToolCredentialId.Value &&
                    credential.TenantId == tool.TenantId,
                cancellationToken))
        {
            return OperationResult.Failure(
                "Credencial de Tool não encontrada para o tenant.");
        }

        try
        {
            tool.Update(
                request.Name,
                request.Description,
                request.Endpoint,
                request.HttpMethod,
                request.InputSchema,
                request.ToolCredentialId);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> ActivateAsync(
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == toolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        tool.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> DeactivateAsync(
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == toolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        tool.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<AgentToolBindingDto>> ListForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            return Array.Empty<AgentToolBindingDto>();
        }

        Guid? tenantId = await _dbContext.AiAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Select(agent => (Guid?)agent.TenantId)
            .SingleOrDefaultAsync(cancellationToken);

        if (tenantId is null)
        {
            return Array.Empty<AgentToolBindingDto>();
        }

        return await (
            from tool in _dbContext.AgentTools.AsNoTracking()
            where tool.TenantId == tenantId.Value
            join binding in _dbContext.AgentToolBindings.AsNoTracking()
                    .Where(candidate =>
                        candidate.AgentId == agentId &&
                        candidate.TenantId == tenantId.Value)
                on tool.Id equals binding.ToolId
                into bindings
            from binding in bindings.DefaultIfEmpty()
            orderby tool.Name
            select new AgentToolBindingDto(
                tool.Id,
                tool.Name,
                tool.Description,
                tool.HttpMethod,
                binding != null && binding.IsActive,
                tool.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OperationResult> BindAsync(
        Guid agentId,
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AiAgent? agent = await _dbContext.AiAgents
            .SingleOrDefaultAsync(
                candidate => candidate.Id == agentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult.Failure(
                "Agente não encontrado.");
        }

        AgentTool? tool = await _dbContext.AgentTools
            .SingleOrDefaultAsync(
                candidate => candidate.Id == toolId,
                cancellationToken);

        if (tool is null)
        {
            return OperationResult.Failure(
                "Tool não encontrada.");
        }

        if (tool.TenantId != agent.TenantId)
        {
            return OperationResult.Failure(
                "A Tool não pertence ao mesmo tenant do agente.");
        }

        if (!tool.IsActive)
        {
            return OperationResult.Failure(
                "Não é possível vincular uma Tool desativada.");
        }

        AgentToolBinding? binding =
            await _dbContext.AgentToolBindings
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.AgentId == agentId &&
                        candidate.ToolId == toolId,
                    cancellationToken);

        if (binding is null)
        {
            binding = new AgentToolBinding(
                agent.TenantId,
                agent.Id,
                tool.Id);

            _dbContext.AgentToolBindings.Add(binding);
        }
        else
        {
            binding.Activate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> UnbindAsync(
        Guid agentId,
        Guid toolId,
        CancellationToken cancellationToken = default)
    {
        AgentToolBinding? binding =
            await _dbContext.AgentToolBindings
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.AgentId == agentId &&
                        candidate.ToolId == toolId,
                    cancellationToken);

        if (binding is null)
        {
            return OperationResult.Failure(
                "Vínculo da Tool com o agente não encontrado.");
        }

        binding.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }
}
