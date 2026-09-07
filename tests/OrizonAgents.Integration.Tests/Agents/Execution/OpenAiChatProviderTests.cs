using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Domain.Agents;
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
        var handler = new RecordingHandler(
            HttpStatusCode.BadRequest,
            sensitiveBody);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateProvider(handler).CompleteAsync(
                    "gpt-test",
                    "secret prompt",
                    "secret message",
                    Array.Empty<AiChatMessage>(),
                    0.5));

        Assert.Contains("400", exception.Message);
        Assert.DoesNotContain(sensitiveBody, exception.Message);
        Assert.DoesNotContain("tenant-openai-key", exception.Message);
        Assert.DoesNotContain("secret prompt", exception.Message);
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
        RecordingHandler handler,
        IAiProviderCredentialService? credentials = null) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/")
            },
            credentials ?? new StubCredentialService("tenant-openai-key"));

    private static int Count(string value, string term) =>
        value.Split(term, StringSplitOptions.None).Length - 1;

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
