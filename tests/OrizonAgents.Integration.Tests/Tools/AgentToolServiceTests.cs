using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Tools.Requests;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tools;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class AgentToolServiceTests
{
    [Fact]
    public async Task BindAsync_AllowsAgentAndToolFromSameTenant()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantId = Guid.NewGuid();

        AiAgent agent = CreateAgent(tenantId);
        AgentTool tool = CreateTool(tenantId);

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result =
            await service.BindAsync(agent.Id, tool.Id);

        Assert.True(result.Succeeded);

        AgentToolBinding? binding =
            await db.AgentToolBindings
                .SingleOrDefaultAsync(candidate =>
                    candidate.AgentId == agent.Id &&
                    candidate.ToolId == tool.Id);

        Assert.NotNull(binding);
        Assert.Equal(tenantId, binding.TenantId);
    }

    [Fact]
    public async Task BindAsync_RejectsToolFromDifferentTenant()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid agentTenantId = Guid.NewGuid();
        Guid toolTenantId = Guid.NewGuid();

        AiAgent agent = CreateAgent(agentTenantId);
        AgentTool tool = CreateTool(toolTenantId);

        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result =
            await service.BindAsync(agent.Id, tool.Id);

        Assert.False(result.Succeeded);

        Assert.False(
            await db.AgentToolBindings.AnyAsync(candidate =>
                candidate.AgentId == agent.Id &&
                candidate.ToolId == tool.Id));
    }

    [Fact]
    public async Task ListForAgentAsync_DoesNotListToolFromDifferentTenant()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();

        AiAgent agent = CreateAgent(tenantA);

        AgentTool sameTenantTool = CreateTool(tenantA);
        AgentTool otherTenantTool = CreateTool(tenantB);

        db.AiAgents.Add(agent);
        db.AgentTools.AddRange(
            sameTenantTool,
            otherTenantTool);

        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result =
            await service.ListForAgentAsync(agent.Id);

        Assert.Contains(
            result,
            item => item.ToolId == sameTenantTool.Id);

        Assert.DoesNotContain(
            result,
            item => item.ToolId == otherTenantTool.Id);
    }

    [Fact]
    public async Task CreateAsync_PreservesExistingHttpBehavior()
    {
        await using ServiceProvider provider = CreateProvider();
        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var capabilities = new StubCapabilityService { Granted = true };
        var service = new AgentToolService(db, capabilities);

        var result = await service.CreateAsync(new CreateAgentToolRequest(
            tenantId, "Consultar API", "Consulta uma API.", "https://example.com/data", "POST",
            "{\"type\":\"object\"}", null, AgentToolRiskLevel.Read));

        Assert.True(result.Succeeded);
        AgentTool tool = await db.AgentTools.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(AgentToolKind.Http, tool.Kind);
        Assert.Equal("https://example.com/data", tool.Endpoint);
        Assert.Equal("POST", tool.HttpMethod);
        Assert.NotNull(tool.InputSchema);
        Assert.Null(tool.IntegrationConnectionId);
        Assert.Equal(0, capabilities.Calls);
    }

    [Theory]
    [InlineData(AgentToolKind.GmailSearch)]
    [InlineData(AgentToolKind.GmailReadMessage)]
    public async Task CreateAsync_CreatesGmailKindWithConnectionAndNoTechnicalConfiguration(AgentToolKind kind)
    {
        await using ServiceProvider provider = CreateProvider();
        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var connection = ConnectedConnection(tenantId);
        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync();
        var capabilities = new StubCapabilityService { Granted = true };
        var service = new AgentToolService(db, capabilities);

        var result = await service.CreateAsync(new CreateAgentToolRequest(
            tenantId, "Gmail", "Ação Gmail.", "", "", "malicious-schema", Guid.NewGuid(),
            AgentToolRiskLevel.Read, kind, connection.Id));

        Assert.True(result.Succeeded);
        AgentTool tool = await db.AgentTools.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(kind, tool.Kind);
        Assert.Equal(connection.Id, tool.IntegrationConnectionId);
        Assert.Equal("GET", tool.HttpMethod);
        Assert.StartsWith("gmail://", tool.Endpoint);
        Assert.Null(tool.InputSchema);
        Assert.Null(tool.ToolCredentialId);
        Assert.Equal(connection.Id, capabilities.ConnectionId);
        Assert.Equal(GoogleOAuthCapability.GmailRead, capabilities.Capability);
    }

    [Theory]
    [InlineData("capability")]
    [InlineData("disconnected")]
    [InlineData("inactive")]
    [InlineData("other-tenant")]
    public async Task CreateAsync_RejectsIneligibleGmailConnection(string reason)
    {
        await using ServiceProvider provider = CreateProvider();
        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        Guid connectionTenantId = reason == "other-tenant" ? Guid.NewGuid() : tenantId;
        var connection = ConnectedConnection(connectionTenantId);
        if (reason == "disconnected") connection.Disconnect();
        if (reason == "inactive") connection.Deactivate();
        db.IntegrationConnections.Add(connection);
        await db.SaveChangesAsync();
        var capabilities = new StubCapabilityService { Granted = reason != "capability" };
        var service = new AgentToolService(db, capabilities);

        var result = await service.CreateAsync(new CreateAgentToolRequest(
            tenantId, "Gmail", "Ação Gmail.", "", "", null, null,
            AgentToolRiskLevel.Read, AgentToolKind.GmailSearch, connection.Id));

        Assert.False(result.Succeeded);
        Assert.False(await db.AgentTools.IgnoreQueryFilters().AnyAsync());
    }

    private static AgentToolService CreateService(OrizonAgentsDbContext db) =>
        new(db, new StubCapabilityService { Granted = true });

    private static IntegrationConnection ConnectedConnection(Guid tenantId)
    {
        var connection = new IntegrationConnection(tenantId, "E-mail Comercial", IntegrationProvider.Gmail);
        connection.Connect("usuario@example.com", "protected-credentials");
        return connection;
    }

    private sealed class StubCapabilityService : IGoogleOAuthCapabilityService
    {
        public bool Granted { get; init; }
        public int Calls { get; private set; }
        public Guid? ConnectionId { get; private set; }
        public GoogleOAuthCapability? Capability { get; private set; }

        public Task<bool> HasCapabilityAsync(
            Guid connectionId,
            GoogleOAuthCapability capability,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            ConnectionId = connectionId;
            Capability = capability;
            return Task.FromResult(Granted);
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(
            provider => provider.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(
            provider => provider.GetRequiredService<CurrentTenant>());

        services.AddDbContext<OrizonAgentsDbContext>(
            options =>
                options.UseInMemoryDatabase(
                    $"AgentToolServiceTests-{Guid.NewGuid()}"));

        return services.BuildServiceProvider();
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
            $"Tool {Guid.NewGuid():N}",
            "Tool usada nos testes.",
            "https://example.com/api/test",
            "POST");
    }
}
