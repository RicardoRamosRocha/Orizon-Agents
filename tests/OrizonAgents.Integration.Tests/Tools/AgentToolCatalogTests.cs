using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Models;
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
        const string inputSchema =
            """{"type":"object","additionalProperties":false}""";
        tool.Update(
            tool.Name,
            tool.Description,
            tool.Endpoint,
            tool.HttpMethod,
            inputSchema,
            null,
            AgentToolRiskLevel.Sensitive);

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
        Assert.Equal(tool.Description, result[0].Description);
        Assert.Equal("POST", result[0].HttpMethod);
        Assert.Equal(inputSchema, result[0].InputSchema);
        Assert.Equal(AgentToolRiskLevel.Sensitive, result[0].RiskLevel);
        Assert.Equal(AgentToolKind.Http, result[0].Kind);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_GmailSearch_UsesDerivedSafeSchema()
    {
        await using ServiceProvider provider = CreateProvider();
        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        var agent = CreateAgent(tenantId);
        var tool = CreateGmailTool(
            tenantId,
            AgentToolKind.GmailSearch,
            connectionId,
            "https://internal.example/sensitive-endpoint",
            "DELETE");
        tool.SetRiskLevel(AgentToolRiskLevel.Sensitive);
        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        db.AgentToolBindings.Add(
            new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();

        AgentToolDefinition definition = Assert.Single(
            await new AgentToolCatalog(db)
                .GetAvailableToolsAsync(agent.Id));

        Assert.Equal(tool.Id, definition.Id);
        Assert.Equal(AgentToolKind.GmailSearch, definition.Kind);
        Assert.Equal(
            AgentToolRiskLevel.Sensitive,
            definition.RiskLevel);
        using JsonDocument schema =
            JsonDocument.Parse(definition.InputSchema!);
        JsonElement root = schema.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Equal(
            "string",
            root.GetProperty("properties")
                .GetProperty("query")
                .GetProperty("type")
                .GetString());
        JsonElement maxResults = root.GetProperty("properties")
            .GetProperty("maxResults");
        Assert.Equal("integer", maxResults.GetProperty("type").GetString());
        Assert.Equal(1, maxResults.GetProperty("minimum").GetInt32());
        Assert.Equal(100, maxResults.GetProperty("maximum").GetInt32());
        Assert.Equal(
            "query",
            Assert.Single(root.GetProperty("required").EnumerateArray())
                .GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        string serialized = JsonSerializer.Serialize(definition);
        Assert.DoesNotContain(connectionId.ToString(), serialized);
        Assert.DoesNotContain(tool.Endpoint, serialized);
        Assert.DoesNotContain("IntegrationConnectionId", serialized);
        Assert.DoesNotContain("ToolCredentialId", serialized);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_GmailReadMessage_UsesDerivedSafeSchema()
    {
        await using ServiceProvider provider = CreateProvider();
        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var agent = CreateAgent(tenantId);
        var tool = CreateGmailTool(
            tenantId,
            AgentToolKind.GmailReadMessage,
            Guid.NewGuid());
        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        db.AgentToolBindings.Add(
            new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();

        AgentToolDefinition definition = Assert.Single(
            await new AgentToolCatalog(db)
                .GetAvailableToolsAsync(agent.Id));

        Assert.Equal(tool.Id, definition.Id);
        Assert.Equal(AgentToolKind.GmailReadMessage, definition.Kind);
        Assert.Equal(AgentToolRiskLevel.Read, definition.RiskLevel);
        using JsonDocument schema =
            JsonDocument.Parse(definition.InputSchema!);
        JsonElement root = schema.RootElement;
        JsonElement properties = root.GetProperty("properties");
        JsonProperty messageId = Assert.Single(
            properties.EnumerateObject());
        Assert.Equal("messageId", messageId.Name);
        Assert.Equal(
            "string",
            messageId.Value.GetProperty("type").GetString());
        Assert.Equal(
            "messageId",
            Assert.Single(root.GetProperty("required").EnumerateArray())
                .GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task GetAvailableToolsAsync_GmailWithExplicitSchema_PreservesConfiguredSchema()
    {
        await using ServiceProvider provider = CreateProvider();
        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var agent = CreateAgent(tenantId);
        var tool = CreateGmailTool(
            tenantId,
            AgentToolKind.GmailSearch,
            Guid.NewGuid());
        const string configuredSchema = """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string" },
            "mailboxAlias": { "type": "string" }
          },
          "required": ["query", "mailboxAlias"],
          "additionalProperties": false
        }
        """;
        tool.Update(
            tool.Name,
            tool.Description,
            tool.Endpoint,
            tool.HttpMethod,
            configuredSchema,
            null,
            AgentToolRiskLevel.Read);
        db.AiAgents.Add(agent);
        db.AgentTools.Add(tool);
        db.AgentToolBindings.Add(
            new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();

        AgentToolDefinition definition = Assert.Single(
            await new AgentToolCatalog(db)
                .GetAvailableToolsAsync(agent.Id));

        Assert.Equal(configuredSchema, definition.InputSchema);
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

    private static AgentTool CreateGmailTool(
        Guid tenantId,
        AgentToolKind kind,
        Guid connectionId,
        string endpoint = "https://example.com/legacy-gmail",
        string httpMethod = "POST")
    {
        var tool = new AgentTool(
            tenantId,
            "Gmail",
            "Opera mensagens Gmail.",
            endpoint,
            httpMethod);

        tool.ConfigureKind(kind, connectionId);
        return tool;
    }
}
