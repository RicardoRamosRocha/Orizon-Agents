using System.Net;
using System.Text;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class GroqChatProviderTests
{
    [Fact]
    public async Task CompleteAsync_UsesResolvedTenantCredential()
    {
        var handler = new RecordingHttpMessageHandler();

        var provider = CreateProvider(
            handler,
            new StubApiKeyResolver("tenant-key"));

        AiChatCompletionResult result = await CompleteAsync(provider);

        Assert.Equal("Resposta Groq", result.Content);
        Assert.Equal(new AiChatUsage(80, 20, 100), result.Usage);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("tenant-key", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task CompleteAsync_FailsWhenNoCredentialCanBeResolved()
    {
        var handler = new RecordingHttpMessageHandler();

        var provider = CreateProvider(
            handler,
            new StubApiKeyResolver(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CompleteAsync(provider));
        Assert.Null(handler.AuthorizationParameter);
    }

    private static Task<AiChatCompletionResult> CompleteAsync(
        GroqChatProvider provider)
    {
        return provider.CompleteAsync(
            "groq-test",
            "system",
            "hello",
            Array.Empty<AiChatMessage>(),
            0.5);
    }

    private static GroqChatProvider CreateProvider(
        HttpMessageHandler handler,
        IAiProviderApiKeyResolver apiKeyResolver)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.groq.com/")
        };

        return new GroqChatProvider(
            client,
            apiKeyResolver);
    }

    private sealed class RecordingHttpMessageHandler :
        HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme =
                request.Headers.Authorization?.Scheme;

            AuthorizationParameter =
                request.Headers.Authorization?.Parameter;

            var response =
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "choices": [
                            {
                              "message": {
                                "content": "Resposta Groq"
                              }
                            }
                          ],
                          "usage": {
                            "prompt_tokens": 80,
                            "completion_tokens": 20,
                            "total_tokens": 100
                          }
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };

            return Task.FromResult(response);
        }
    }

    private sealed class StubApiKeyResolver(string? apiKey)
        : IAiProviderApiKeyResolver
    {
        public Task<string?> ResolveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(apiKey);
        }
    }
}
