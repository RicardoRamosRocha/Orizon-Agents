using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tools;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class AgentToolCatalogTests
{
    [Fact]
    public async Task GetAvailableToolsAsync_ReturnsToolFromSameTenant()
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

        var catalog = new AgentToolCatalog(db);

        var result =
            await catalog.GetAvailableToolsAsync(agent.Id);

        Assert.Single(result);
        Assert.Equal(tool.Id, result[0].Id);
        Assert.Equal(tool.Name, result[0].Name);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_DoesNotReturnCrossTenantTool()
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

        // Simula um vínculo inconsistente/malicioso:
        // o agente pertence a um tenant e a Tool a outro.
        db.AgentToolBindings.Add(
            new AgentToolBinding(
                toolTenantId,
                agent.Id,
                tool.Id));

        await db.SaveChangesAsync();

        var catalog = new AgentToolCatalog(db);

        var result =
            await catalog.GetAvailableToolsAsync(agent.Id);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_ReturnsEmptyForEmptyAgentId()
    {
        await using ServiceProvider provider = CreateProvider();

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        var catalog = new AgentToolCatalog(db);

        var result =
            await catalog.GetAvailableToolsAsync(Guid.Empty);

        Assert.Empty(result);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

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
            "Status operacional",
            "Consulta o status operacional.",
            "https://example.com/status",
            "POST");
    }
}
