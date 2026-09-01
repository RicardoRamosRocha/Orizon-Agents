using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Domain.Agents;
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

        var service = new AgentToolService(db);

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

        var service = new AgentToolService(db);

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

        var service = new AgentToolService(db);

        var result =
            await service.ListForAgentAsync(agent.Id);

        Assert.Contains(
            result,
            item => item.ToolId == sameTenantTool.Id);

        Assert.DoesNotContain(
            result,
            item => item.ToolId == otherTenantTool.Id);
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
