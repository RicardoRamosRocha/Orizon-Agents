using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Agents.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Agents;

public sealed class AiAgentService : IAiAgentService
{
    private readonly OrizonAgentsDbContext _dbContext;

    public AiAgentService(OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AiAgentListItemDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AiAgents
            .AsNoTracking()
            .OrderBy(agent => agent.Name)
            .Select(agent => new AiAgentListItemDto(
                agent.Id,
                agent.Name,
                agent.Description,
                agent.Provider.ToString(),
                agent.Model,
                agent.IsActive,
                agent.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AiAgentDetailsDto?> GetAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AiAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Select(agent => new AiAgentDetailsDto(
                agent.Id,
                agent.TenantId,
                agent.Name,
                agent.Description,
                agent.SystemPrompt,
                agent.Provider.ToString(),
                agent.Model,
                agent.Temperature,
                agent.IsActive,
                agent.CreatedAtUtc,
                agent.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateAiAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseProvider(request.Provider, out AiProvider provider))
        {
            return OperationResult<Guid>.Failure("Provedor de IA inválido.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<Guid>.Failure("Informe o nome do agente.");
        }

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            return OperationResult<Guid>.Failure("Informe as instruções do agente.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return OperationResult<Guid>.Failure("Informe o modelo de IA.");
        }

        var agent = new AiAgent(
            request.TenantId,
            request.Name,
            request.SystemPrompt,
            provider,
            request.Model);

        agent.Update(
            request.Name,
            request.Description,
            request.SystemPrompt,
            provider,
            request.Model,
            request.Temperature);

        _dbContext.AiAgents.Add(agent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<Guid>.Success(agent.Id);
    }

    public async Task<OperationResult> UpdateAsync(
        UpdateAiAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        AiAgent? agent = await _dbContext.AiAgents
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.AgentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult.Failure("Agente não encontrado.");
        }

        if (!TryParseProvider(request.Provider, out AiProvider provider))
        {
            return OperationResult.Failure("Provedor de IA inválido.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.SystemPrompt) ||
            string.IsNullOrWhiteSpace(request.Model))
        {
            return OperationResult.Failure(
                "Nome, instruções e modelo são obrigatórios.");
        }

        agent.Update(
            request.Name,
            request.Description,
            request.SystemPrompt,
            provider,
            request.Model,
            request.Temperature);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> ActivateAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        AiAgent? agent = await _dbContext.AiAgents
            .SingleOrDefaultAsync(
                candidate => candidate.Id == agentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult.Failure("Agente não encontrado.");
        }

        agent.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> DeactivateAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        AiAgent? agent = await _dbContext.AiAgents
            .SingleOrDefaultAsync(
                candidate => candidate.Id == agentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult.Failure("Agente não encontrado.");
        }

        agent.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult.Success();
    }

    private static bool TryParseProvider(
        string value,
        out AiProvider provider)
    {
        return Enum.TryParse(value, true, out provider) &&
               Enum.IsDefined(provider);
    }
}
