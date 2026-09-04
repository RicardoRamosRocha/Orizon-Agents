using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Integrations.Gmail;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class AgentToolExecutorGmailTests
{
    [Fact]
    public async Task GmailSearch_UsesServerConnection_IgnoresModelConnection_AndNeverCallsHttp()
    {
        await using var fixture = new Fixture();
        Guid serverConnectionId = Guid.NewGuid();
        Guid modelConnectionId = Guid.NewGuid();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            serverConnectionId,
            endpoint: "http://127.0.0.1/private",
            httpMethod: "DELETE");
        fixture.Gmail.SearchResult = new GmailSearchResult(
            [new GmailMessageReference("message-1", "thread-1")],
            "next-page",
            1);
        JsonElement input = Json(
            $$"""
            {
              "query": "is:unread",
              "maxResults": 25,
              "connectionId": "{{modelConnectionId}}"
            }
            """);

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(agent.Id, tool.Id, input));

        Assert.True(result.Succeeded);
        Assert.Equal(serverConnectionId, fixture.Gmail.ConnectionId);
        Assert.NotEqual(modelConnectionId, fixture.Gmail.ConnectionId);
        Assert.Equal("is:unread", fixture.Gmail.Query);
        Assert.Equal(25, fixture.Gmail.MaxResults);
        Assert.Equal(1, fixture.Gmail.SearchCalls);
        Assert.Equal(0, fixture.Http.CallCount);
        using JsonDocument content = JsonDocument.Parse(result.Content!);
        Assert.Equal(
            "message-1",
            content.RootElement
                .GetProperty("messages")[0]
                .GetProperty("id")
                .GetString());
        Assert.Equal(
            "next-page",
            content.RootElement.GetProperty("nextPageToken").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"query":"   "}""")]
    public async Task GmailSearch_RequiresNonBlankQuery(string json)
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid());

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(agent.Id, tool.Id, Json(json)));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "argumentos",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GmailSearch_RejectsUnsafeMaxResults(int maxResults)
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid());
        JsonElement input = Json(
            $$"""{"query":"is:unread","maxResults":{{maxResults}}}""");

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(agent.Id, tool.Id, input));

        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
    }

    [Fact]
    public async Task GmailSearch_WhenMaxResultsIsOmitted_UsesSafeDefault()
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid());

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.True(result.Succeeded);
        Assert.Equal(10, fixture.Gmail.MaxResults);
    }

    [Fact]
    public async Task GmailReadMessage_UsesServerConnection_AndReturnsOnlyMessageFields()
    {
        await using var fixture = new Fixture();
        Guid serverConnectionId = Guid.NewGuid();
        Guid modelConnectionId = Guid.NewGuid();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailReadMessage,
            serverConnectionId);
        fixture.Gmail.Message = new GmailMessage(
            "message-1",
            "thread-1",
            "Assunto",
            "from@example.com",
            "to@example.com",
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
            "Resumo",
            "Corpo");
        JsonElement input = Json(
            $$"""
            {
              "messageId": "message-1",
              "connectionId": "{{modelConnectionId}}"
            }
            """);

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(agent.Id, tool.Id, input));

        Assert.True(result.Succeeded);
        Assert.Equal(serverConnectionId, fixture.Gmail.ConnectionId);
        Assert.NotEqual(modelConnectionId, fixture.Gmail.ConnectionId);
        Assert.Equal("message-1", fixture.Gmail.MessageId);
        Assert.Equal(1, fixture.Gmail.ReadCalls);
        Assert.Equal(0, fixture.Http.CallCount);
        using JsonDocument content = JsonDocument.Parse(result.Content!);
        string[] properties = content.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Assert.Equal(
            ["id", "threadId", "subject", "from", "to", "date", "snippet", "bodyText"],
            properties);
        Assert.Equal(
            "Corpo",
            content.RootElement.GetProperty("bodyText").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"messageId":" "}""")]
    public async Task GmailReadMessage_RequiresMessageId(string json)
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailReadMessage,
            Guid.NewGuid());

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(agent.Id, tool.Id, Json(json)));

        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Gmail.ReadCalls);
    }

    [Fact]
    public async Task GmailTool_WithMissingServerConnection_FailsSafely()
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid());
        typeof(AgentTool)
            .GetProperty(nameof(AgentTool.IntegrationConnectionId))!
            .SetValue(tool, null);
        await fixture.Db.SaveChangesAsync();

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "conexão",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
        Assert.Equal(0, fixture.Http.CallCount);
    }

    [Fact]
    public async Task GmailTool_NotBoundToAgent_DoesNotExecute()
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid(),
            createBinding: false);

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "não vinculada",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GmailTool_InactiveToolOrBinding_DoesNotExecute(
        bool deactivateTool)
    {
        await using var fixture = new Fixture();
        var (agent, tool, binding) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid());

        if (deactivateTool)
        {
            tool.Deactivate();
        }
        else
        {
            binding!.Deactivate();
        }

        await fixture.Db.SaveChangesAsync();

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
    }

    [Fact]
    public async Task GmailTool_CrossTenantBinding_DoesNotExecute()
    {
        await using var fixture = new Fixture();
        Guid otherTenantId = Guid.NewGuid();
        var agent = Fixture.CreateAgent(fixture.TenantId);
        var tool = Fixture.CreateTool(
            otherTenantId,
            AgentToolKind.GmailSearch,
            Guid.NewGuid());
        var binding = new AgentToolBinding(
            otherTenantId,
            agent.Id,
            tool.Id);
        fixture.Db.AiAgents.Add(agent);
        fixture.Db.AgentTools.Add(tool);
        fixture.Db.AgentToolBindings.Add(binding);
        await fixture.Db.SaveChangesAsync();

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.False(result.Succeeded);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
        Assert.Equal(0, fixture.Http.CallCount);
    }

    [Fact]
    public async Task GmailTool_InputSchemaValidation_HappensBeforeExecution()
    {
        const string schema = """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string" },
            "requiredBySchema": { "type": "string" }
          },
          "required": ["query", "requiredBySchema"],
          "additionalProperties": false
        }
        """;
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid(),
            inputSchema: schema);

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "argumentos",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
    }

    [Fact]
    public async Task GmailTool_SensitiveRisk_RequiresApprovalBeforeExecution()
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailReadMessage,
            Guid.NewGuid(),
            sensitive: true);

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"messageId":"message-1"}""")));

        Assert.True(result.RequiresApproval);
        Assert.NotNull(result.ApprovalId);
        Assert.Equal(0, fixture.Gmail.ReadCalls);
        ToolExecutionApproval approval =
            await fixture.Db.ToolExecutionApprovals.SingleAsync();
        Assert.Equal(agent.Id, approval.AgentId);
        Assert.Equal(tool.Id, approval.ToolId);
        Assert.Equal(
            ToolExecutionApprovalStatus.Pending,
            approval.Status);
    }

    [Fact]
    public async Task GmailFailure_DoesNotExposeProviderContentOrToken()
    {
        const string token = "SENSITIVE-GOOGLE-ACCESS-TOKEN";
        const string providerBody = "SENSITIVE-GMAIL-PROVIDER-BODY";
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.GmailSearch,
            Guid.NewGuid());
        fixture.Gmail.Exception =
            new InvalidOperationException($"{token} {providerBody}");

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(
                agent.Id,
                tool.Id,
                Json("""{"query":"is:unread"}""")));

        Assert.False(result.Succeeded);
        Assert.Null(result.Content);
        Assert.DoesNotContain(token, result.Error ?? string.Empty);
        Assert.DoesNotContain(providerBody, result.Error ?? string.Empty);
        string logs = string.Join(" ", fixture.GmailLogger.Messages);
        Assert.DoesNotContain(token, logs);
        Assert.DoesNotContain(providerBody, logs);
    }

    [Fact]
    public async Task HttpTool_StillUsesExistingHttpExecutionPath()
    {
        await using var fixture = new Fixture();
        var (agent, tool, _) = await fixture.SeedAsync(
            AgentToolKind.Http,
            connectionId: null);

        AgentToolExecutionResult result = await fixture.Executor.ExecuteAsync(
            new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.True(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("""{"http":true}""", result.Content);
        Assert.Equal(1, fixture.Http.CallCount);
        Assert.Equal(0, fixture.Gmail.SearchCalls);
        Assert.Equal(0, fixture.Gmail.ReadCalls);
    }

    private static JsonElement Json(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture()
        {
            TenantId = Guid.NewGuid();
            Tenant.SetTenantId(TenantId);

            var options =
                new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;

            Db = new OrizonAgentsDbContext(options, Tenant);

            var httpExecutor = new HttpAgentToolExecutor(
                new StubHttpClientFactory(Http),
                new AgentToolEndpointPolicy(
                    Options.Create(new AgentToolHttpOptions())),
                new StubToolCredentialService(),
                Options.Create(new AgentToolHttpOptions()),
                NullLogger<HttpAgentToolExecutor>.Instance);

            var gmailExecutor = new GmailAgentToolExecutor(
                Gmail,
                GmailLogger);

            Executor = new AgentToolExecutor(
                Db,
                new Infrastructure.Tools.Validation.AgentToolInputValidator(),
                new ToolExecutionApprovalService(Db, Tenant),
                httpExecutor,
                gmailExecutor,
                NullLogger<AgentToolExecutor>.Instance);
        }

        public Guid TenantId { get; }
        public CurrentTenant Tenant { get; } = new();
        public OrizonAgentsDbContext Db { get; }
        public StubGmailClient Gmail { get; } = new();
        public RecordingHttpMessageHandler Http { get; } = new();
        public RecordingLogger<GmailAgentToolExecutor> GmailLogger { get; } = new();
        public AgentToolExecutor Executor { get; }

        public async Task<(
            AiAgent Agent,
            AgentTool Tool,
            AgentToolBinding? Binding)> SeedAsync(
                AgentToolKind kind,
                Guid? connectionId,
                string endpoint = "https://example.com/tool",
                string httpMethod = "POST",
                bool createBinding = true,
                string? inputSchema = null,
                bool sensitive = false)
        {
            AiAgent agent = CreateAgent(TenantId);
            AgentTool tool = CreateTool(
                TenantId,
                kind,
                connectionId,
                endpoint,
                httpMethod);

            if (inputSchema is not null)
            {
                tool.Update(
                    tool.Name,
                    tool.Description,
                    tool.Endpoint,
                    tool.HttpMethod,
                    inputSchema,
                    null,
                    tool.RiskLevel);
            }

            if (sensitive)
            {
                tool.SetRiskLevel(AgentToolRiskLevel.Sensitive);
            }

            AgentToolBinding? binding = createBinding
                ? new AgentToolBinding(
                    TenantId,
                    agent.Id,
                    tool.Id)
                : null;

            Db.AiAgents.Add(agent);
            Db.AgentTools.Add(tool);
            if (binding is not null)
            {
                Db.AgentToolBindings.Add(binding);
            }

            await Db.SaveChangesAsync();
            return (agent, tool, binding);
        }

        public static AiAgent CreateAgent(Guid tenantId) =>
            new(
                tenantId,
                "Agente de teste",
                "Você é um agente de teste.",
                AiProvider.GoogleGemini,
                "gemini-test");

        public static AgentTool CreateTool(
            Guid tenantId,
            AgentToolKind kind,
            Guid? connectionId,
            string endpoint = "https://example.com/tool",
            string httpMethod = "POST")
        {
            var tool = new AgentTool(
                tenantId,
                "Tool de teste",
                "Executa uma operação de teste.",
                endpoint,
                httpMethod);

            if (kind != AgentToolKind.Http)
            {
                tool.ConfigureKind(kind, connectionId);
            }

            return tool;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class StubGmailClient : IGmailClient
    {
        public GmailSearchResult SearchResult { get; set; } =
            new([], null, 0);

        public GmailMessage Message { get; set; } =
            new(
                "message-id",
                "thread-id",
                null,
                null,
                null,
                null,
                null,
                null);

        public Exception? Exception { get; set; }
        public int SearchCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public Guid? ConnectionId { get; private set; }
        public string? Query { get; private set; }
        public int? MaxResults { get; private set; }
        public string? MessageId { get; private set; }

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

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(SearchResult);
        }

        public Task<GmailMessage> GetMessageAsync(
            Guid connectionId,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            ConnectionId = connectionId;
            MessageId = messageId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Message);
        }
    }

    private sealed class StubHttpClientFactory(
        HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler);

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"http":true}""")
                });
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                Messages.Add(exception.Message);
            }
        }
    }
}
