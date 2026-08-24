using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class AiAgentRunner : IAiAgentRunner
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IEnumerable<IAiChatProvider> _providers;

    public AiAgentRunner(
        OrizonAgentsDbContext dbContext,
        IEnumerable<IAiChatProvider> providers)
    {
        _dbContext = dbContext;
        _providers = providers;
    }

    public async Task<OperationResult<string>> RunAsync(
        Guid agentId,
        string userMessage,
        IReadOnlyList<AiChatMessage>? history = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return OperationResult<string>.Failure(
                "Digite uma mensagem para o agente.");
        }

        AiAgent? agent = await _dbContext.AiAgents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == agentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult<string>.Failure(
                "Agente não encontrado.");
        }

        if (!agent.IsActive)
        {
            return OperationResult<string>.Failure(
                "Este agente está desativado.");
        }

        IAiChatProvider? provider = _providers
            .FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ProviderName,
                    agent.Provider.ToString(),
                    StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return OperationResult<string>.Failure(
                $"O provedor {agent.Provider} ainda não está disponível.");
        }

        try
        {
            string response = await provider.CompleteAsync(
                agent.Model,
                agent.SystemPrompt,
                userMessage.Trim(),
                history ?? Array.Empty<AiChatMessage>(),
                agent.Temperature,
                cancellationToken);

            return OperationResult<string>.Success(response);
        }
        catch (Exception)
        {
            return OperationResult<string>.Failure(
                "Não foi possível obter uma resposta da Inteligência Artificial.");
        }
    }
}
