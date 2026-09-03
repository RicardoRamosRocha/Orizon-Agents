using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge.Retrieval;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Agents.Execution;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class AiAgentRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenToolRequiresApproval_DoesNotCallProviderAgain()
    {
        var options =
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        Guid tenantId = Guid.NewGuid();

        var currentTenant = new CurrentTenant();
        currentTenant.SetTenantId(tenantId);

        await using var db =
            new OrizonAgentsDbContext(
                options,
                currentTenant);

        var agent = new AiAgent(
            tenantId,
            "Agente de teste",
            "Você é um agente de teste.",
            AiProvider.GoogleGemini,
            "test-model");

        db.AiAgents.Add(agent);
        await db.SaveChangesAsync();

        Guid toolId = Guid.NewGuid();
        Guid approvalId = Guid.NewGuid();

        var provider = new CountingChatProvider(
            AiProvider.GoogleGemini.ToString(),
            """{"type":"tool_call"}""");

        var toolCatalog =
            new StubToolCatalog(
                new AgentToolDefinition(
                    toolId,
                    "Tool sensível",
                    "Executa operação sensível.",
                    "POST",
                    null,
                    AgentToolRiskLevel.Sensitive));

        var knowledgeRetriever =
            new EmptyKnowledgeRetriever();

        var toolExecutor =
            new ApprovalRequiredToolExecutor(
                approvalId);

        JsonElement input;

        using (JsonDocument document =
            JsonDocument.Parse(
                """{"amount":100}"""))
        {
            input = document.RootElement.Clone();
        }

        var decisionParser =
            new StubDecisionParser(
                AgentModelDecision.RequestTool(
                    new AgentToolCall(
                        toolId,
                        input)));

        var runner = new AiAgentRunner(
            db,
            new[] { provider },
            toolCatalog,
            knowledgeRetriever,
            toolExecutor,
            decisionParser,
            NullLogger<AiAgentRunner>.Instance);

        OperationResult<AiAgentRunResult> result =
            await runner.RunAsync(
                agent.Id,
                new AgentRunRequest(
                    "Execute a operação sensível."));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);

        Assert.Equal(
            AiAgentRunStatus.ApprovalRequired,
            result.Value.Status);

        Assert.True(
            result.Value.RequiresApproval);

        Assert.Equal(
            approvalId,
            result.Value.ApprovalId);

        Assert.Equal(
            "Esta ação requer aprovação humana antes de ser executada.",
            result.Value.Response);

        Assert.Equal(
            1,
            provider.CallCount);

        Assert.Equal(
            1,
            toolExecutor.CallCount);
    }

    private sealed class CountingChatProvider :
        IAiChatProvider
    {
        private readonly string _response;

        public CountingChatProvider(
            string providerName,
            string response)
        {
            ProviderName = providerName;
            _response = response;
        }

        public string ProviderName { get; }

        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(
            string model,
            string systemPrompt,
            string userMessage,
            IReadOnlyList<AiChatMessage> history,
            double temperature,
            string? operationalContext = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(_response);
        }
    }

    private sealed class StubToolCatalog :
        IAgentToolCatalog
    {
        private readonly IReadOnlyList<AgentToolDefinition> _tools;

        public StubToolCatalog(
            params AgentToolDefinition[] tools)
        {
            _tools = tools;
        }

        public Task<IReadOnlyList<AgentToolDefinition>>
            GetAvailableToolsAsync(
                Guid agentId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tools);
        }
    }

    private sealed class EmptyKnowledgeRetriever :
        IKnowledgeRetriever
    {
        public Task<IReadOnlyList<KnowledgeRetrievalResult>>
            RetrieveAsync(
                Guid agentId,
                string query,
                int maxResults = 5,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<KnowledgeRetrievalResult> results =
                Array.Empty<KnowledgeRetrievalResult>();

            return Task.FromResult(results);
        }
    }

    private sealed class ApprovalRequiredToolExecutor :
        IAgentToolExecutor
    {
        private readonly Guid _approvalId;

        public ApprovalRequiredToolExecutor(
            Guid approvalId)
        {
            _approvalId = approvalId;
        }

        public int CallCount { get; private set; }

        public Task<AgentToolExecutionResult> ExecuteAsync(
            AgentToolExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(
                AgentToolExecutionResult.ApprovalRequired(
                    _approvalId));
        }
    }

    private sealed class StubDecisionParser :
        IAgentModelDecisionParser
    {
        private readonly AgentModelDecision _decision;

        public StubDecisionParser(
            AgentModelDecision decision)
        {
            _decision = decision;
        }

        public AgentModelDecision Parse(
            string modelResponse)
        {
            return _decision;
        }
    }
}
