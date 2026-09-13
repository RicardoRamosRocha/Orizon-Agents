using System.Net;
using System.Text;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class OpenAiModelCatalogTests
{
    [Fact]
    public async Task ListAsync_UsesTenantCredentialAndParsesModels()
    {
        var handler = new CatalogHandler(
            HttpStatusCode.OK,
            """
            {
              "object": "list",
              "data": [
                { "id": "model-z", "object": "model" },
                { "id": "model-a", "object": "model" }
              ]
            }
            """);
        var credentials =
            new OpenAiChatProviderTests.StubCredentialService("tenant-key");
        var catalog = new OpenAiModelCatalog(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/")
            },
            credentials);

        IReadOnlyList<AiProviderModel> models = await catalog.ListAsync();

        Assert.Equal(AiProvider.OpenAI, catalog.Provider);
        Assert.Equal(["model-a", "model-z"], models.Select(x => x.Id));
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("https://api.openai.com/v1/models", handler.Uri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("tenant-key", handler.AuthorizationParameter);
        Assert.Equal(AiProvider.OpenAI, credentials.LastProvider);
    }

    [Fact]
    public async Task ListAsync_HttpError_DoesNotExposeBodyOrKey()
    {
        const string sensitiveBody = "SENSITIVE MODEL RESPONSE";
        var handler = new CatalogHandler(
            HttpStatusCode.Forbidden,
            sensitiveBody);
        var catalog = new OpenAiModelCatalog(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.openai.com/")
            },
            new OpenAiChatProviderTests.StubCredentialService("tenant-key"));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => catalog.ListAsync());

        Assert.Contains("403", exception.Message);
        Assert.DoesNotContain(sensitiveBody, exception.Message);
        Assert.DoesNotContain("tenant-key", exception.Message);
    }

    private sealed class CatalogHandler(
        HttpStatusCode statusCode,
        string responseBody) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
