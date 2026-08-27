using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class AiAgentRunner : IAiAgentRunner
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IEnumerable<IAiChatProvider> _providers;
    private readonly IAgentToolCatalog _toolCatalog;
    private readonly IAgentToolExecutor _toolExecutor;
    private readonly IAgentModelDecisionParser _decisionParser;
    private readonly ILogger<AiAgentRunner> _logger;

    public AiAgentRunner(
        OrizonAgentsDbContext dbContext,
        IEnumerable<IAiChatProvider> providers,
        IAgentToolCatalog toolCatalog,
        IAgentToolExecutor toolExecutor,
        IAgentModelDecisionParser decisionParser,
        ILogger<AiAgentRunner> logger)
    {
        _dbContext = dbContext;
        _providers = providers;
        _toolCatalog = toolCatalog;
        _toolExecutor = toolExecutor;
        _decisionParser = decisionParser;
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

            IReadOnlyList<AgentToolDefinition> availableTools =
                await _toolCatalog.GetAvailableToolsAsync(
                    agent.Id,
                    cancellationToken);

            string effectiveSystemPrompt =
                BuildSystemPromptWithTools(
                    agent.SystemPrompt,
                    availableTools);

            string modelResponse = await provider.CompleteAsync(
                agent.Model,
                effectiveSystemPrompt,
                normalizedMessage,
                history,
                agent.Temperature,
                request.Context?.GetRawText(),
                cancellationToken);

            AgentModelDecision decision =
                _decisionParser.Parse(modelResponse);

            string response = modelResponse;

            if (decision.Type == AgentModelDecisionType.ToolCall &&
                decision.ToolCall is not null)
            {
                AgentToolExecutionResult toolResult =
                    await _toolExecutor.ExecuteAsync(
                        new AgentToolExecutionRequest(
                            agent.Id,
                            decision.ToolCall.ToolId,
                            decision.ToolCall.Input),
                        cancellationToken);

                string toolContext = BuildToolResultContext(
                    decision.ToolCall,
                    toolResult);

                response = await provider.CompleteAsync(
                    agent.Model,
                    BuildSystemPromptAfterToolExecution(
                        agent.SystemPrompt),
                    normalizedMessage,
                    history,
                    agent.Temperature,
                    toolContext,
                    cancellationToken);
            }

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

    private static string BuildToolResultContext(
        AgentToolCall toolCall,
        AgentToolExecutionResult result)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine(
            "Resultado de uma ferramenta executada pelo sistema:");
        builder.AppendLine($"ToolId: {toolCall.ToolId}");
        builder.AppendLine($"Sucesso: {result.Succeeded}");

        if (result.StatusCode.HasValue)
        {
            builder.AppendLine(
                $"HTTP Status: {result.StatusCode.Value}");
        }

        if (!string.IsNullOrWhiteSpace(result.Content))
        {
            builder.AppendLine("Conteúdo retornado:");
            builder.AppendLine(result.Content);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            builder.AppendLine("Erro:");
            builder.AppendLine(result.Error);
        }

        builder.AppendLine();
        builder.AppendLine(
            "Use este resultado para responder ao usuário. " +
            "Não invente informações que não estejam presentes no resultado.");

        return builder.ToString();
    }

    private static string BuildSystemPromptAfterToolExecution(
        string systemPrompt)
    {
        return systemPrompt +
            "\n\nUma ferramenta solicitada anteriormente já foi executada " +
            "pelo sistema. Analise o resultado fornecido no contexto operacional " +
            "e produza agora a resposta final para o usuário. " +
            "Não solicite outra ferramenta nesta etapa.";
    }

    private static string BuildSystemPromptWithTools(
        string systemPrompt,
        IReadOnlyList<AgentToolDefinition> tools)
    {
        if (tools.Count == 0)
        {
            return systemPrompt;
        }

        var builder = new System.Text.StringBuilder();

        builder.AppendLine(systemPrompt);
        builder.AppendLine();
        builder.AppendLine("Ferramentas disponíveis para este agente:");

        foreach (AgentToolDefinition tool in tools)
        {
            builder.AppendLine();
            builder.AppendLine($"- Nome: {tool.Name}");
            builder.AppendLine($"  Id: {tool.Id}");
            builder.AppendLine($"  Descrição: {tool.Description}");
            builder.AppendLine($"  Método HTTP: {tool.HttpMethod}");

            if (!string.IsNullOrWhiteSpace(tool.InputSchema))
            {
                builder.AppendLine(
                    $"  Schema de entrada: {tool.InputSchema}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Regras para uso das ferramentas:");
        builder.AppendLine(
            "1. Use uma ferramenta somente quando ela for necessária " +
            "para responder corretamente à solicitação do usuário.");
        builder.AppendLine(
            "2. Se não precisar de ferramenta, responda normalmente.");
        builder.AppendLine(
            "3. Se precisar executar uma ferramenta, responda SOMENTE " +
            "com um objeto JSON válido, sem Markdown, sem bloco de código " +
            "e sem qualquer texto adicional.");
        builder.AppendLine(
            "4. O JSON deve seguir exatamente este formato:");
        builder.AppendLine(
            "{\"action\":\"tool_call\",\"toolId\":\"GUID_DA_TOOL\"," +
            "\"input\":{}}");
        builder.AppendLine(
            "5. Use exclusivamente o Id de uma das ferramentas listadas acima.");
        builder.AppendLine(
            "6. Preencha input de acordo com o schema da ferramenta, " +
            "quando houver.");
        builder.AppendLine(
            "7. Nunca afirme que executou uma ferramenta. A execução é " +
            "responsabilidade do sistema.");

        return builder.ToString();
    }

    private static string CreateConversationTitle(string message)
    {
        string normalized = message.Trim();

        return normalized.Length <= 80
            ? normalized
            : normalized[..80];
    }
}



