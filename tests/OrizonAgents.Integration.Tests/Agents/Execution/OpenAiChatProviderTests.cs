using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure;
using OrizonAgents.Infrastructure.Agents.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class OpenAiChatProviderTests
{
    [Fact]
    public async Task CompleteAsync_SendsResponsesRequestAndParsesContentAndUsage()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """
            {
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [
                    { "type": "output_text", "text": "Primeira parte" },
                    { "type": "output_text", "text": "Segunda parte" }
                  ]
                }
              ],
              "usage": {
                "input_tokens": 120,
                "output_tokens": 30,
                "total_tokens": 150
              }
            }
            """);
        var credentials = new StubCredentialService("tenant-openai-key");
        var provider = CreateProvider(handler, credentials);

        AiChatCompletionResult result = await provider.CompleteAsync(
            "gpt-test",
            "SYSTEM PROMPT",
            "CURRENT USER MESSAGE",
            [
                new AiChatMessage("user", "HISTORY USER"),
                new AiChatMessage("assistant", "HISTORY ASSISTANT"),
                new AiChatMessage("ignored", "IGNORED HISTORY")
            ],
            0.4,
            "OPERATIONAL CONTEXT");

        Assert.Equal("OpenAI", provider.ProviderName);
        Assert.Equal("Primeira parte\nSegunda parte", result.Content);
        Assert.Equal(new AiChatUsage(120, 30, 150), result.Usage);
        Assert.Equal(AiProvider.OpenAI, credentials.LastProvider);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.openai.com/v1/responses", handler.Uri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("tenant-openai-key", handler.AuthorizationParameter);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonElement root = request.RootElement;
        Assert.Equal("gpt-test", root.GetProperty("model").GetString());
        Assert.Equal(0.4, root.GetProperty("temperature").GetDouble());
        Assert.False(root.GetProperty("store").GetBoolean());
        string instructions = root.GetProperty("instructions").GetString()!;
        Assert.Contains("SYSTEM PROMPT", instructions);
        Assert.Contains("OPERATIONAL CONTEXT", instructions);
        Assert.Equal(1, Count(instructions, "OPERATIONAL CONTEXT"));

        JsonElement input = root.GetProperty("input");
        Assert.Equal(3, input.GetArrayLength());
        Assert.Equal("user", input[0].GetProperty("role").GetString());
        Assert.Equal("HISTORY USER", input[0].GetProperty("content").GetString());
        Assert.Equal("assistant", input[1].GetProperty("role").GetString());
        Assert.Equal("HISTORY ASSISTANT", input[1].GetProperty("content").GetString());
        Assert.Equal("CURRENT USER MESSAGE", input[2].GetProperty("content").GetString());
        Assert.DoesNotContain("IGNORED HISTORY", handler.RequestBody);
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.Empty(result.ToolCalls);
    }

    [Fact]
    public async Task CompleteWithToolsAsync_TranslatesGmailSearchAndParsesArguments()
    {
        Guid toolId = Guid.NewGuid();
        const string secret = "SENSITIVE-CONNECTION-DATA";
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """
            {
              "id": "resp_123",
              "output": [
                { "type": "reasoning" },
                { "type": "message", "content": [] },
                {
                  "type": "function_call",
                  "call_id": "call_123",
                  "name": "orizon_tool_1",
                  "arguments": "{\"query\":\"newer_than:7d\",\"maxResults\":3}"
                }
              ]
            }
            """);
        var diagnosticLogger =
            new RecordingLogger<OpenAiChatProvider>();
        var tool = new AgentToolDefinition(
            toolId,
            "GmailSearch",
            "Pesquisa as mensagens que correspondem à consulta.",
            $"DELETE {secret} https://gmail.internal/private",
            """
            {
              "type": "object",
              "properties": {
                "query": { "type": "string" },
                "maxResults": { "type": "integer" }
              },
              "required": ["query"],
              "additionalProperties": false
            }
            """,
            AgentToolRiskLevel.Sensitive,
            AgentToolKind.GmailSearch);

        AiChatCompletionResult result = await CreateProvider(
                handler,
                logger: diagnosticLogger)
            .CompleteWithToolsAsync(
                "gpt-4.1-mini",
                "system",
                "Procure os 3 e-mails mais recentes.",
                [],
                0.2,
                [tool]);

        AgentToolCall call = Assert.Single(result.ToolCalls);
        Assert.Equal(toolId, call.ToolId);
        Assert.Equal("newer_than:7d", call.Input?.GetProperty("query").GetString());
        Assert.Equal(3, call.Input?.GetProperty("maxResults").GetInt32());
        Assert.Equal(string.Empty, result.Content);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonElement root = request.RootElement;
        JsonElement nativeTool = Assert.Single(root.GetProperty("tools").EnumerateArray());
        Assert.Equal("function", nativeTool.GetProperty("type").GetString());
        Assert.Equal("orizon_tool_1", nativeTool.GetProperty("name").GetString());

        string toolDescription = nativeTool.GetProperty("description").GetString()!;
        Assert.Contains("conta Gmail conectada e autorizada", toolDescription);
        Assert.Contains("query é opcional", toolDescription);
        Assert.Contains("mensagens recentes sem filtro", toolDescription);
        Assert.Contains("Subject e From", toolDescription);
        Assert.Contains("sem ler o corpo", toolDescription);
        Assert.Equal("object", nativeTool.GetProperty("parameters").GetProperty("type").GetString());
        Assert.True(nativeTool.GetProperty("parameters").GetProperty("properties").TryGetProperty("query", out _));
        Assert.Equal("auto", root.GetProperty("tool_choice").GetString());
        Assert.True(root.GetProperty("parallel_tool_calls").GetBoolean());
        Assert.DoesNotContain(toolId.ToString(), handler.RequestBody);
        Assert.DoesNotContain(secret, handler.RequestBody);
        Assert.DoesNotContain("gmail.internal", handler.RequestBody);

        string logs = string.Join(Environment.NewLine, diagnosticLogger.Messages);
        Assert.Contains("ReceivedToolCount: 1", logs);
        Assert.Contains("OpenAiToolDefinitionCount: 1", logs);
        Assert.Contains("OpenAiFunctionNames: orizon_tool_1", logs);
        Assert.Contains("ToolChoice: auto", logs);
        Assert.Contains("ParallelToolCalls: True", logs);
        Assert.Contains("HttpStatusCode: 200", logs);
        Assert.Contains("ResponseId: resp_123", logs);
        Assert.Contains(
            "OutputTypes: reasoning, message, function_call",
            logs);
        Assert.Contains("FunctionCallCount: 1", logs);
        Assert.Contains("FunctionNames: orizon_tool_1", logs);
        Assert.Contains("AgentToolCallCount: 1", logs);
        Assert.DoesNotContain("newer_than:7d", logs);
        Assert.DoesNotContain("Procure os 3 e-mails mais recentes.", logs);
        Assert.DoesNotContain(secret, logs);
    }

    [Fact]
    public async Task CompleteWithToolsAsync_ParsesMultipleFunctionCallsInOrder()
    {
        Guid firstToolId = Guid.NewGuid();
        Guid secondToolId = Guid.NewGuid();
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """
            {
              "output": [
                {
                  "type": "function_call",
                  "call_id": "call_1",
                  "name": "orizon_tool_1",
                  "arguments": "{\"messageId\":\"message-1\"}"
                },
                {
                  "type": "function_call",
                  "call_id": "call_2",
                  "name": "orizon_tool_2",
                  "arguments": "{\"messageId\":\"message-2\"}"
                }
              ]
            }
            """);

        AiChatCompletionResult result = await CreateProvider(handler)
            .CompleteWithToolsAsync(
                "gpt-test",
                "system",
                "user",
                [],
                0.5,
                [
                    CreateTool(firstToolId),
                    CreateTool(secondToolId)
                ]);

        Assert.Collection(
            result.ToolCalls,
            call =>
            {
                Assert.Equal(firstToolId, call.ToolId);
                Assert.Equal("message-1", call.Input?.GetProperty("messageId").GetString());
                Assert.Equal("call_1", call.CorrelationId);
                Assert.Equal("call_1", call.CorrelationId);
            },
            call =>
            {
                Assert.Equal(secondToolId, call.ToolId);
                Assert.Equal("message-2", call.Input?.GetProperty("messageId").GetString());
                Assert.Equal("call_2", call.CorrelationId);
                Assert.Equal("call_2", call.CorrelationId);
            });
    }

    [Fact]
    public async Task ContinueWithToolsAsync_StatelessContinuationIncludesOriginalUserMessageAndToolOutput()
    {
        Guid toolId = Guid.NewGuid();
        var handler = new SequencedRecordingHandler(
            """
            {
              "id": "resp_123",
              "output": [
                {
                  "type": "function_call",
                  "call_id": "call_123",
                  "name": "orizon_tool_1",
                  "arguments": "{\"maxResults\":3}"
                }
              ]
            }
            """,
            """
            {
              "id": "resp_456",
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [
                    { "type": "output_text", "text": "Encontrei tres mensagens." }
                  ]
                }
              ]
            }
            """);
        OpenAiChatProvider provider = CreateProvider(handler);
        AgentToolDefinition tool = CreateTool(toolId);
        const string originalUserMessage =
            "Procure no meu Gmail os 3 e-mails mais recentes e informe assunto e remetente.";
        const string operationalContext = "RAG_CONTEXT: política interna aplicável.";

        AiChatCompletionResult first = await provider.CompleteWithToolsAsync(
            "gpt-4.1-mini", "system", originalUserMessage, [], 0.2, [tool],
            operationalContext);
        AgentToolCall call = Assert.Single(first.ToolCalls);
        Assert.Equal("call_123", call.CorrelationId);

        AiChatCompletionResult second = await provider.ContinueWithToolsAsync(
            "gpt-4.1-mini",
            "system",
            0.2,
            [tool],
            first.ContinuationToken!,
            [new AgentToolResult(
                call.CorrelationId!,
                "{\"messages\":[{\"id\":\"message-1\",\"subject\":\"Subject 1\",\"from\":\"sender@example.test\"}]}")],
            operationalContext);

        Assert.Equal("Encontrei tres mensagens.", second.Content);
        Assert.Equal(2, handler.CallCount);

        using JsonDocument continuationRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        JsonElement root = continuationRequest.RootElement;
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.False(root.TryGetProperty("previous_response_id", out _));

        string instructions = root.GetProperty("instructions").GetString()!;
        Assert.Contains(operationalContext, instructions);
        Assert.Equal(1, Count(instructions, operationalContext));

        JsonElement input = root.GetProperty("input");
        Assert.Contains(
            input.EnumerateArray(),
            item => item.TryGetProperty("role", out JsonElement role) &&
                    role.GetString() == "user" &&
                    item.GetProperty("content").GetString() == originalUserMessage);
        Assert.Contains(
            input.EnumerateArray(),
            item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "function_call" &&
                    item.GetProperty("call_id").GetString() == "call_123" &&
                    item.GetProperty("name").GetString() == "orizon_tool_1");
        Assert.Contains(
            input.EnumerateArray(),
            item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "function_call_output" &&
                    item.GetProperty("call_id").GetString() == "call_123" &&
                    item.GetProperty("output").GetString()!.Contains("Subject 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ContinueWithToolsAsync_MultipleRoundsPreservesOperationalContextWithoutDuplication()
    {
        Guid toolId = Guid.NewGuid();
        var handler = new SequencedRecordingHandler(
            """{"output":[{"type":"function_call","call_id":"call_1","name":"orizon_tool_1","arguments":"{}"}]}""",
            """{"output":[{"type":"function_call","call_id":"call_2","name":"orizon_tool_1","arguments":"{}"}]}""",
            """{"output":[{"type":"message","content":[{"type":"output_text","text":"Resposta final"}]}]}""");
        OpenAiChatProvider provider = CreateProvider(handler);
        AgentToolDefinition tool = CreateTool(toolId);
        const string userMessage = "Pergunta original";
        const string operationalContext = "RAG_CONTEXT: dado não confiável.";

        AiChatCompletionResult first = await provider.CompleteWithToolsAsync(
            "gpt-test", "system", userMessage, [], 0.2, [tool], operationalContext);
        AiChatCompletionResult second = await provider.ContinueWithToolsAsync(
            "gpt-test", "system", 0.2, [tool], first.ContinuationToken!,
            [new AgentToolResult("call_1", "resultado-1")], operationalContext);
        AiChatCompletionResult third = await provider.ContinueWithToolsAsync(
            "gpt-test", "system", 0.2, [tool], second.ContinuationToken!,
            [new AgentToolResult("call_2", "resultado-2")], operationalContext);

        Assert.Equal("Resposta final", third.Content);
        Assert.Equal(3, handler.CallCount);

        foreach (string requestBody in handler.RequestBodies)
        {
            using JsonDocument request = JsonDocument.Parse(requestBody);
            JsonElement root = request.RootElement;
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.False(root.TryGetProperty("previous_response_id", out _));
            Assert.Equal(1, Count(root.GetProperty("instructions").GetString()!, operationalContext));
        }

        using JsonDocument finalRequest = JsonDocument.Parse(handler.RequestBodies[2]);
        Assert.Contains(
            finalRequest.RootElement.GetProperty("input").EnumerateArray(),
            item => item.TryGetProperty("role", out JsonElement role) &&
                    role.GetString() == "user" &&
                    item.GetProperty("content").GetString() == userMessage);
    }

    [Fact]
    public async Task CompleteAsync_WhenUsageIsAbsent_ReturnsNullUsage()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """
            {
              "output": [{
                "type": "message",
                "content": [{ "type": "output_text", "text": "Resposta" }]
              }]
            }
            """);

        AiChatCompletionResult result = await CreateProvider(handler)
            .CompleteAsync(
                "gpt-test",
                "system",
                "user",
                Array.Empty<AiChatMessage>(),
                0.5);

        Assert.Equal("Resposta", result.Content);
        Assert.Null(result.Usage);
    }

    [Fact]
    public async Task CompleteAsync_HttpError_DoesNotExposeResponseBodyOrKey()
    {
        const string sensitiveBody = "SENSITIVE OPENAI RESPONSE";
        var diagnosticLogger = new RecordingLogger<OpenAiChatProvider>();
        var handler = new RecordingHandler(
            HttpStatusCode.BadRequest,
            sensitiveBody);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateProvider(handler, logger: diagnosticLogger).CompleteAsync(
                    "gpt-test",
                    "secret prompt",
                    "secret message",
                    Array.Empty<AiChatMessage>(),
                    0.5));

        Assert.Contains("400", exception.Message);
        Assert.DoesNotContain(sensitiveBody, exception.Message);
        Assert.DoesNotContain("tenant-openai-key", exception.Message);
        Assert.DoesNotContain("secret prompt", exception.Message);

        string logs = string.Join(Environment.NewLine, diagnosticLogger.Messages);
        Assert.Contains("HttpStatusCode: 400", logs);
        Assert.DoesNotContain(sensitiveBody, logs);
        Assert.DoesNotContain("secret prompt", logs);
        Assert.DoesNotContain("secret message", logs);
        Assert.DoesNotContain("tenant-openai-key", logs);
    }

    [Fact]
    public async Task CompleteAsync_UsesOnlyTenantCredential()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var credentials = new StubCredentialService(null);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateProvider(handler, credentials).CompleteAsync(
                    "gpt-test",
                    "system",
                    "user",
                    Array.Empty<AiChatMessage>(),
                    0.5));

        Assert.Contains("tenant", exception.Message);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(AiProvider.OpenAI, credentials.LastProvider);
    }

    [Fact]
    public void AddInfrastructure_ResolvesOpenAiProviderAndCatalog()
    {
        string keysPath = Path.Combine(
            Path.GetTempPath(),
            $"orizon-openai-tests-{Guid.NewGuid():N}");
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=orizon_test;Username=test;Password=test",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["DataProtection:KeysPath"] = keysPath
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddInfrastructure(configuration, addWebSecurity: false);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();

        Assert.Contains(
            scope.ServiceProvider.GetServices<IAiChatProvider>(),
            provider => provider is OpenAiChatProvider);
        Assert.Contains(
            scope.ServiceProvider.GetServices<
                OrizonAgents.Application.Agents.Models.IAiProviderSpecificModelCatalog>(),
            catalog => catalog is OpenAiModelCatalog);
    }

    private static OpenAiChatProvider CreateProvider(
        HttpMessageHandler handler,
        IAiProviderCredentialService? credentials = null,
        ILogger<OpenAiChatProvider>? logger = null) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/")
            },
            credentials ?? new StubCredentialService("tenant-openai-key"),
            logger ?? NullLogger<OpenAiChatProvider>.Instance);

    private static AgentToolDefinition CreateTool(Guid id) =>
        new(
            id,
            "GmailReadMessage",
            "Lê uma mensagem do Gmail.",
            "GET",
            """
            {
              "type": "object",
              "properties": {
                "messageId": { "type": "string" }
              },
              "required": ["messageId"],
              "additionalProperties": false
            }
            """,
            AgentToolRiskLevel.Read,
            AgentToolKind.GmailReadMessage);

    private static int Count(string value, string term) =>
        value.Split(term, StringSplitOptions.None).Length - 1;

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
        }
    }

    private sealed class SequencedRecordingHandler(params string[] responseBodies)
        : HttpMessageHandler
    {
        private int _nextResponse;

        public int CallCount { get; private set; }
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (_nextResponse >= responseBodies.Length)
            {
                throw new InvalidOperationException("Unexpected OpenAI request.");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseBodies[_nextResponse++],
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
    private sealed class RecordingHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            Uri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    internal sealed class StubCredentialService(string? apiKey)
        : IAiProviderCredentialService
    {
        public AiProvider? LastProvider { get; private set; }

        public Task<string?> ResolveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            LastProvider = provider;
            return Task.FromResult(apiKey);
        }

        public Task SaveAsync(AiProvider provider, string apiKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> HasCredentialAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(!string.IsNullOrWhiteSpace(apiKey));

        public Task RemoveAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
