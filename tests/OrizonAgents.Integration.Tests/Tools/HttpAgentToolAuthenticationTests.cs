using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class HttpAgentToolAuthenticationTests
{
    [Fact]
    public async Task ExecuteAsync_ToolWithoutCredential_RemainsSupported()
    {
        await using ServiceProvider provider = CreateProvider();
        (AiAgent agent, AgentTool tool) = await SeedAsync(provider, null);
        var credentialService = new StubToolCredentialService();
        var handler = new RecordingHandler();

        AgentToolExecutionResult result = await CreateExecutor(provider, credentialService, handler)
            .ExecuteAsync(new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, credentialService.ResolveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ApiKeyHeader_SendsConfiguredHeader()
    {
        await using ServiceProvider provider = CreateProvider();
        (AiAgent agent, AgentTool tool) = await SeedAsync(provider, Guid.NewGuid());
        var credentialService = new StubToolCredentialService
        {
            ResolvedCredential = new ResolvedToolCredential(
                ToolAuthenticationType.ApiKeyHeader,
                "X-Orizon-Api-Key",
                "api-secret-value")
        };
        var handler = new RecordingHandler();

        AgentToolExecutionResult result = await CreateExecutor(provider, credentialService, handler)
            .ExecuteAsync(new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.True(result.Succeeded);
        Assert.Equal("api-secret-value", handler.Headers["X-Orizon-Api-Key"]);
    }

    [Fact]
    public async Task ExecuteAsync_BearerToken_SendsAuthorizationBearer()
    {
        await using ServiceProvider provider = CreateProvider();
        (AiAgent agent, AgentTool tool) = await SeedAsync(provider, Guid.NewGuid());
        var credentialService = new StubToolCredentialService
        {
            ResolvedCredential = new ResolvedToolCredential(
                ToolAuthenticationType.BearerToken,
                "Authorization",
                "bearer-secret-value")
        };
        var handler = new RecordingHandler();

        AgentToolExecutionResult result = await CreateExecutor(provider, credentialService, handler)
            .ExecuteAsync(new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.True(result.Succeeded);
        Assert.Equal("Bearer bearer-secret-value", handler.Headers["Authorization"]);
    }

    [Fact]
    public async Task ExecuteAsync_UnavailableCredential_BlocksWithoutHttpCallOrSecretLeak()
    {
        await using ServiceProvider provider = CreateProvider();
        (AiAgent agent, AgentTool tool) = await SeedAsync(provider, Guid.NewGuid());
        var handler = new RecordingHandler();

        AgentToolExecutionResult result = await CreateExecutor(
                provider,
                new StubToolCredentialService(),
                handler)
            .ExecuteAsync(new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.CallCount);
        Assert.DoesNotContain("secret", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidAuthentication_DoesNotCallHttpOrExposeSecret()
    {
        const string secret = "highly-sensitive-secret-value";
        await using ServiceProvider provider = CreateProvider();
        (AiAgent agent, AgentTool tool) = await SeedAsync(provider, Guid.NewGuid());
        var credentialService = new StubToolCredentialService
        {
            ResolvedCredential = new ResolvedToolCredential(
                (ToolAuthenticationType)999,
                "X-Invalid",
                secret)
        };
        var handler = new RecordingHandler();

        AgentToolExecutionResult result = await CreateExecutor(provider, credentialService, handler)
            .ExecuteAsync(new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.False(result.Succeeded);
        Assert.Equal(0, handler.CallCount);
        Assert.DoesNotContain(secret, result.Error ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, result.Content ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_BlockedEndpoint_DoesNotResolveCredentialOrCallHttp()
    {
        await using ServiceProvider provider = CreateProvider();
        (AiAgent agent, AgentTool tool) = await SeedAsync(
            provider,
            Guid.NewGuid(),
            "http://127.0.0.1/private");
        var credentialService = new StubToolCredentialService();
        var handler = new RecordingHandler();

        AgentToolExecutionResult result = await CreateExecutor(provider, credentialService, handler)
            .ExecuteAsync(new AgentToolExecutionRequest(agent.Id, tool.Id));

        Assert.False(result.Succeeded);
        Assert.Equal(0, credentialService.ResolveCalls);
        Assert.Equal(0, handler.CallCount);
    }

    private static async Task<(AiAgent Agent, AgentTool Tool)> SeedAsync(
        ServiceProvider provider,
        Guid? credentialId,
        string endpoint = "https://example.com/tool")
    {
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var agent = new AiAgent(
            tenantId,
            "Agente de teste",
            "Você é um agente de teste.",
            AiProvider.GoogleGemini,
            "gemini-test");
        var tool = new AgentTool(
            tenantId,
            "Tool autenticada",
            "Tool de teste.",
            endpoint,
            "POST");
        tool.SetCredential(credentialId);

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        db.AgentToolBindings.Add(new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();
        return (agent, tool);
    }

    private static HttpAgentToolExecutor CreateExecutor(
        ServiceProvider provider,
        StubToolCredentialService credentialService,
        RecordingHandler handler)
    {
        return new HttpAgentToolExecutor(
            provider.GetRequiredService<OrizonAgentsDbContext>(),
            new StubHttpClientFactory(handler),
            new AgentToolEndpointPolicy(Options.Create(new AgentToolHttpOptions())),
            credentialService,
            Options.Create(new AgentToolHttpOptions()),
            provider.GetRequiredService<ILogger<HttpAgentToolExecutor>>());
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(x => x.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(x => x.GetRequiredService<CurrentTenant>());
        services.AddDbContext<OrizonAgentsDbContext>(options =>
            options.UseInMemoryDatabase($"ToolAuthentication-{Guid.NewGuid()}"));
        return services.BuildServiceProvider();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public StubHttpClientFactory(HttpMessageHandler handler) => _client = new HttpClient(handler);
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            foreach (var header in request.Headers)
            {
                Headers[header.Key] = string.Join(" ", header.Value);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        }
    }
}
