using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class GroqChatProviderTests
{
    [Fact]
    public async Task CompleteAsync_PrefersTenantCredentialOverConfiguration()
    {
        var handler = new RecordingHttpMessageHandler();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GROQ_API_KEY"] = "configuration-key"
                    })
                .Build();

        var provider = CreateProvider(
            handler,
            configuration,
            new StubCredentialService("tenant-key"));

        string result = await CompleteAsync(provider);

        Assert.Equal("Resposta Groq", result);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("tenant-key", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task CompleteAsync_UsesConfigurationWhenTenantCredentialIsMissing()
    {
        var handler = new RecordingHttpMessageHandler();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["GROQ_API_KEY"] = "configuration-key"
                    })
                .Build();

        var provider = CreateProvider(
            handler,
            configuration,
            new StubCredentialService(null));

        string result = await CompleteAsync(provider);

        Assert.Equal("Resposta Groq", result);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("configuration-key", handler.AuthorizationParameter);
    }

    private static Task<string> CompleteAsync(
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
        IConfiguration configuration,
        IAiProviderCredentialService credentialService)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.groq.com/")
        };

        return new GroqChatProvider(
            client,
            configuration,
            credentialService);
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
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };

            return Task.FromResult(response);
        }
    }

    private sealed class StubCredentialService :
        IAiProviderCredentialService
    {
        private readonly string? _apiKey;

        public StubCredentialService(string? apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> ResolveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiKey);
        }

        public Task SaveAsync(
            AiProvider provider,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasCredentialAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                !string.IsNullOrWhiteSpace(_apiKey));
        }

        public Task RemoveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
