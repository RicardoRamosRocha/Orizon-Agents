using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class HttpAgentToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RejectsToolNotBoundToAgent()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantId = Guid.NewGuid();

        var agent = new AiAgent(
            tenantId,
            "Agente A",
            "Você é um agente de teste.",
            AiProvider.GoogleGemini,
            "gemini-test");

        var otherAgent = new AiAgent(
            tenantId,
            "Agente B",
            "Você é outro agente.",
            AiProvider.GoogleGemini,
            "gemini-test");

        var tool = new AgentTool(
            tenantId,
            "Status",
            "Consulta o status.",
            "https://example.com/status",
            "POST");

        db.AiAgents.AddRange(agent, otherAgent);
        db.AgentTools.Add(tool);

        db.AgentToolBindings.Add(
            new AgentToolBinding(
                tenantId,
                otherAgent.Id,
                tool.Id));

        await db.SaveChangesAsync();

        var executor = CreateExecutor(provider);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    agent.Id,
                    tool.Id));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "não vinculada",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInactiveTool()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantId = Guid.NewGuid();

        var agent = CreateAgent(tenantId);

        var tool = CreateTool(tenantId);
        tool.Deactivate();

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);

        db.AgentToolBindings.Add(
            new AgentToolBinding(
                tenantId,
                agent.Id,
                tool.Id));

        await db.SaveChangesAsync();

        var executor = CreateExecutor(provider);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    agent.Id,
                    tool.Id));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "inativa",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsInactiveBinding()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantId = Guid.NewGuid();

        var agent = CreateAgent(tenantId);
        var tool = CreateTool(tenantId);

        var binding = new AgentToolBinding(
            tenantId,
            agent.Id,
            tool.Id);

        binding.Deactivate();

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        db.AgentToolBindings.Add(binding);

        await db.SaveChangesAsync();

        var executor = CreateExecutor(provider);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    agent.Id,
                    tool.Id));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "não vinculada",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsCrossTenantBinding()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid agentTenantId = Guid.NewGuid();
        Guid toolTenantId = Guid.NewGuid();

        var agent = CreateAgent(agentTenantId);
        var tool = CreateTool(toolTenantId);

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);

        db.AgentToolBindings.Add(
            new AgentToolBinding(
                toolTenantId,
                agent.Id,
                tool.Id));

        await db.SaveChangesAsync();

        var executor = CreateExecutor(provider);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    agent.Id,
                    tool.Id));

        Assert.False(result.Succeeded);

        Assert.Contains(
            "não vinculada",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ExecuteAsync_RejectsEmptyAgentId()
    {
        await using ServiceProvider provider = CreateProvider();

        var executor = CreateExecutor(provider);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    Guid.Empty,
                    Guid.NewGuid()));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "AgentId",
            result.Error ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsEmptyToolId()
    {
        await using ServiceProvider provider = CreateProvider();

        var executor = CreateExecutor(provider);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    Guid.NewGuid(),
                    Guid.Empty));

        Assert.False(result.Succeeded);
        Assert.Contains(
            "ToolId",
            result.Error ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SendsPostJsonAndReturnsSuccessfulResponse()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantId = Guid.NewGuid();

        var agent = CreateAgent(tenantId);

        var tool = new AgentTool(
            tenantId,
            "Consultar status",
            "Consulta o status operacional.",
            "https://example.com/status",
            "POST");

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);

        db.AgentToolBindings.Add(
            new AgentToolBinding(
                tenantId,
                agent.Id,
                tool.Id));

        await db.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.OK,
            """{"status":"operational","code":"ORIZON-7429"}""");

        var executor = CreateExecutor(
            provider,
            new StubHttpClientFactory(handler));

        using JsonDocument inputDocument =
            JsonDocument.Parse(
                """{"source":"integration-test"}""");

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    agent.Id,
                    tool.Id,
                    inputDocument.RootElement.Clone()));

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.StatusCode);

        Assert.Contains(
            "ORIZON-7429",
            result.Content ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Equal(
            HttpMethod.Post,
            handler.RequestMethod);

        Assert.Equal(
            "https://example.com/status",
            handler.RequestUri?.ToString());

        Assert.Equal(
            "application/json",
            handler.ContentType);

        Assert.Contains(
            "integration-test",
            handler.RequestBody ?? string.Empty,
            StringComparison.Ordinal);
    }


    [Fact]
    public async Task ExecuteAsync_RejectsResponseLargerThanMaximumAllowed()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantId = Guid.NewGuid();

        var agent = CreateAgent(tenantId);
        var tool = CreateTool(tenantId);

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);

        db.AgentToolBindings.Add(
            new AgentToolBinding(
                tenantId,
                agent.Id,
                tool.Id));

        await db.SaveChangesAsync();

        string oversizedResponse =
            new('x', (256 * 1024) + 1);

        var handler =
            new RecordingHttpMessageHandler(
                HttpStatusCode.OK,
                oversizedResponse);

        var httpClientFactory =
            new StubHttpClientFactory(handler);

        var executor =
            CreateExecutor(
                provider,
                httpClientFactory);

        AgentToolExecutionResult result =
            await executor.ExecuteAsync(
                new AgentToolExecutionRequest(
                    agent.Id,
                    tool.Id));

        Assert.False(result.Succeeded);
        Assert.Null(result.Content);

        Assert.Contains(
            "tamanho máximo",
            result.Error ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            (int)HttpStatusCode.OK,
            result.StatusCode);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddScoped<CurrentTenant>();

        services.AddScoped<ICurrentTenant>(
            provider =>
                provider.GetRequiredService<CurrentTenant>());

        services.AddScoped<ITenantContextSetter>(
            provider =>
                provider.GetRequiredService<CurrentTenant>());

        services.AddDbContext<OrizonAgentsDbContext>(
            options =>
                options.UseInMemoryDatabase(
                    Guid.NewGuid().ToString()));

        services.AddHttpClient();

        return services.BuildServiceProvider();
    }

    private static HttpAgentToolExecutor CreateExecutor(
        ServiceProvider provider,
        IHttpClientFactory? httpClientFactory = null)
    {
        var endpointPolicy =
            new AgentToolEndpointPolicy(
                Options.Create(
                    new AgentToolHttpOptions()));

        return new HttpAgentToolExecutor(
            provider.GetRequiredService<OrizonAgentsDbContext>(),
            httpClientFactory ??
                provider.GetRequiredService<IHttpClientFactory>(),
            endpointPolicy,
            new StubToolCredentialService(),
            Options.Create(
                new AgentToolHttpOptions()),
            provider.GetRequiredService<
                ILogger<HttpAgentToolExecutor>>());
    }

    private static AiAgent CreateAgent(Guid tenantId)
    {
        return new AiAgent(
            tenantId,
            "Agente de teste",
            "Você é um agente de teste.",
            AiProvider.GoogleGemini,
            "gemini-test");
    }

    private static AgentTool CreateTool(Guid tenantId)
    {
        return new AgentTool(
            tenantId,
            "Status operacional",
            "Consulta o status operacional.",
            "https://example.com/status",
            "POST");
    }

    private sealed class StubHttpClientFactory :
        IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(
            HttpMessageHandler handler)
        {
            _client = new HttpClient(handler);
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private sealed class RecordingHttpMessageHandler :
        HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public RecordingHttpMessageHandler(
            HttpStatusCode statusCode,
            string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        public HttpMethod? RequestMethod { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        public string? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri;

            if (request.Content is not null)
            {
                RequestBody =
                    await request.Content.ReadAsStringAsync(
                        cancellationToken);

                ContentType =
                    request.Content.Headers.ContentType?.MediaType;
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
        }
    }

}
