using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Execution.Telemetry;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Knowledge.Retrieval;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Agents.Execution;
using OrizonAgents.Infrastructure.Agents.Execution.Context;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools;
using OrizonAgents.Infrastructure.Tools.Execution;
using OrizonAgents.Infrastructure.Tools.Validation;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class AiAgentRunnerRealToolTests
{
    [Fact]
    public async Task RunAsync_WithBoundGmailSearch_ExecutesRealToolAndReturnsResultToLlm()
    {
        Guid tenantId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        var currentTenant = new CurrentTenant();
        currentTenant.SetTenantId(tenantId);
        DbContextOptions<OrizonAgentsDbContext> options =
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase($"RealTool-{Guid.NewGuid():N}")
                .Options;
        await using var db = new OrizonAgentsDbContext(options, currentTenant);

        var agent = new AiAgent(
            tenantId,
            "Agente Gmail",
            "Use GmailSearch quando a pergunta depender de e-mails.",
            AiProvider.GoogleGemini,
            "test-model");
        var tool = new AgentTool(
            tenantId,
            "Pesquisar Gmail",
            "Pesquisa mensagens Gmail.",
            "gmail://search",
            "GET");
        tool.ConfigureKind(AgentToolKind.GmailSearch, connectionId);
        tool.SetRiskLevel(AgentToolRiskLevel.Read);
        db.AddRange(agent, tool, new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();

        var gmail = new RecordingGmailClient();
        var provider = new TextProtocolProvider(
            $"{{\"action\":\"tool_call\",\"toolId\":\"{tool.Id}\",\"input\":{{\"query\":\"from:finance\",\"maxResults\":1}}}}",
            "Encontrei o relatório financeiro enviado por Financeiro.");
        var executor = new AgentToolExecutor(
            db,
            new AgentToolInputValidator(),
            new AllowToolExecutionApprovalService(),
            new HttpAgentToolExecutor(
                null!,
                null!,
                null!,
                Options.Create(new AgentToolHttpOptions()),
                NullLogger<HttpAgentToolExecutor>.Instance),
            new GmailAgentToolExecutor(
                gmail,
                new GrantedGoogleOAuthCapabilityService(),
                NullLogger<GmailAgentToolExecutor>.Instance),
            NullLogger<AgentToolExecutor>.Instance);
        var runner = new AiAgentRunner(
            db,
            [provider],
            new AgentToolCatalog(db),
            new EmptyKnowledgeRetriever(),
            executor,
            new AgentModelDecisionParser(),
            new AgentContextBudget(),
            new NoOpExecutionTelemetry(),
            currentTenant,
            NullLogger<AiAgentRunner>.Instance);

        OperationResult<AiAgentRunResult> result = await runner.RunAsync(
            agent.Id,
            new AgentRunRequest("Procure o relatório financeiro."));

        Assert.True(result.Succeeded, result.FirstError);
        Assert.Equal(
            "Encontrei o relatório financeiro enviado por Financeiro.",
            result.Value!.Response);
        Assert.Equal(1, gmail.SearchCalls);
        Assert.Equal(connectionId, gmail.ConnectionId);
        Assert.Equal("from:finance", gmail.Query);
        Assert.Equal(1, gmail.MaxResults);
        Assert.Equal(2, provider.CallCount);
        Assert.Contains(
            "finance@example.com",
            provider.OperationalContexts[1],
            StringComparison.Ordinal);
        Assert.Equal(2, await db.AiConversationMessages.CountAsync());
    }

    private sealed class TextProtocolProvider(params string[] responses) : IAiChatProvider
    {
        private readonly Queue<string> _responses = new(responses);

        public string ProviderName => AiProvider.GoogleGemini.ToString();

        public AiChatToolInvocationMode ToolInvocationMode =>
            AiChatToolInvocationMode.TextProtocol;

        public int CallCount { get; private set; }

        public List<string?> OperationalContexts { get; } = [];

        public Task<AiChatCompletionResult> CompleteAsync(
            string model,
            string systemPrompt,
            string userMessage,
            IReadOnlyList<AiChatMessage> history,
            double temperature,
            string? operationalContext = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AiChatCompletionResult> CompleteWithToolsAsync(
            string model,
            string systemPrompt,
            string userMessage,
            IReadOnlyList<AiChatMessage> history,
            double temperature,
            IReadOnlyList<AgentToolDefinition> tools,
            string? operationalContext = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            OperationalContexts.Add(operationalContext);
            return Task.FromResult(
                new AiChatCompletionResult(_responses.Dequeue()));
        }
    }

    private sealed class RecordingGmailClient : IGmailClient
    {
        public int SearchCalls { get; private set; }
        public Guid ConnectionId { get; private set; }
        public string? Query { get; private set; }
        public int MaxResults { get; private set; }

        public Task<GmailSearchResult> SearchMessagesAsync(
            Guid connectionId,
            string query,
            int maxResults = 10,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            ConnectionId = connectionId;
            Query = query;
            MaxResults = maxResults;
            return Task.FromResult(
                new GmailSearchResult(
                    [new GmailMessageReference(
                        "message-1",
                        "thread-1",
                        "Relatório financeiro",
                        "finance@example.com")],
                    null,
                    1));
        }

        public Task<GmailMessage> GetMessageAsync(
            Guid connectionId,
            string messageId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GmailDraft> CreateDraftAsync(
            Guid connectionId,
            string to,
            string subject,
            string body,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GmailSentMessage> SendDraftAsync(
            Guid connectionId,
            string draftId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GmailReplyMessage> ReplyAsync(
            Guid connectionId,
            string messageId,
            string body,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class GrantedGoogleOAuthCapabilityService : IGoogleOAuthCapabilityService
    {
        public Task<bool> HasCapabilityAsync(
            Guid connectionId,
            GoogleOAuthCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class AllowToolExecutionApprovalService : IToolExecutionApprovalService
    {
        public Task<ToolExecutionAuthorizationResult> AuthorizeAsync(
            Guid agentId,
            AgentTool tool,
            AgentToolBinding binding,
            JsonElement? input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ToolExecutionAuthorizationResult.Allowed());

        public Task<IReadOnlyList<ToolExecutionApprovalListItemDto>> ListPendingAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolExecutionApprovalListItemDto>>([]);

        public Task<bool> ApproveAsync(
            Guid approvalId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<ToolExecutionApprovalResult> ApproveAndGetExecutionAsync(
            Guid approvalId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RejectAsync(
            Guid approvalId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class EmptyKnowledgeRetriever : IHybridKnowledgeRetriever
    {
        public Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
            Guid agentId,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRetrievalResult>>([]);
    }

    private sealed class NoOpExecutionTelemetry : IAgentExecutionTelemetry, IAgentExecutionTelemetrySession
    {
        public IAgentExecutionTelemetrySession Start(AgentExecutionTelemetryStart start) => this;
        public void RecordModelCall() { }
        public void RecordModelUsage(AiChatUsage? usage) { }
        public void RecordToolExecution(bool succeeded, bool approvalRequired) { }
        public void RecordRagResults(int count) { }
        public void RecordToolContext(int originalCharacters, int usedCharacters) { }
        public Task CompleteAsync(Guid? conversationId, bool succeeded, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
