using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<AiAgentRunner> _logger;

    public AiAgentRunner(
        OrizonAgentsDbContext dbContext,
        IEnumerable<IAiChatProvider> providers,
        ILogger<AiAgentRunner> logger)
    {
        _dbContext = dbContext;
        _providers = providers;
        _logger = logger;
    }

    public async Task<OperationResult<AiAgentRunResult>> RunAsync(
        Guid agentId,
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return OperationResult<AiAgentRunResult>.Failure(
                "Digite uma mensagem para o agente.");
        }

        AiAgent? agent = await _dbContext.AiAgents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == agentId,
                cancellationToken);

        if (agent is null)
        {
            return OperationResult<AiAgentRunResult>.Failure(
                "Agente não encontrado.");
        }

        if (!agent.IsActive)
        {
            return OperationResult<AiAgentRunResult>.Failure(
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
            return OperationResult<AiAgentRunResult>.Failure(
                $"O provedor {agent.Provider} ainda não está disponível.");
        }

        AiConversation? conversation = null;

        if (request.ConversationId.HasValue)
        {
            conversation = await _dbContext.AiConversations
                .AsNoTracking()
                .Include(candidate => candidate.Messages)
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.Id == request.ConversationId.Value &&
                        candidate.AgentId == agentId,
                    cancellationToken);

            if (conversation is null)
            {
                return OperationResult<AiAgentRunResult>.Failure(
                    "Conversa não encontrada.");
            }
        }

        if (conversation is null)
        {
            string title = CreateConversationTitle(request.Message);

            conversation = new AiConversation(
                agent.TenantId,
                agent.Id,
                title);

            _dbContext.AiConversations.Add(conversation);
        }

        IReadOnlyList<AiChatMessage> history =
            conversation.Messages
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => new AiChatMessage(
                    message.Role == AiMessageRole.User
                        ? "user"
                        : "assistant",
                    message.Content))
                .ToList();

        try
        {
            string normalizedMessage = request.Message.Trim();

            string response = await provider.CompleteAsync(
                agent.Model,
                agent.SystemPrompt,
                normalizedMessage,
                history,
                agent.Temperature,
                request.Context?.GetRawText(),
                cancellationToken);

            AiConversationMessage userMessageEntity =
                conversation.AddUserMessage(normalizedMessage);

            AiConversationMessage assistantMessageEntity =
                conversation.AddAssistantMessage(response);

            if (request.ConversationId.HasValue)
            {
                _dbContext.AiConversationMessages.Add(userMessageEntity);
                _dbContext.AiConversationMessages.Add(assistantMessageEntity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return OperationResult<AiAgentRunResult>.Success(
                new AiAgentRunResult(
                    conversation.Id,
                    response));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Erro ao executar agente {AgentId} na conversa {ConversationId}.",
                agentId,
                conversation?.Id);

            return OperationResult<AiAgentRunResult>.Failure(
                "Não foi possível obter uma resposta da Inteligência Artificial.");
        }
    }

    private static string CreateConversationTitle(string message)
    {
        string normalized = message.Trim();

        return normalized.Length <= 80
            ? normalized
            : normalized[..80];
    }
}



