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
        Assert.Equal(
            GmailToolPolicy.MaximumSearchResultsForAgent,
            maxResults.GetProperty("maximum").GetInt32());
        JsonElement properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("query", out JsonElement query));
        Assert.Equal("string", query.GetProperty("type").GetString());
        Assert.Contains(
            "opcional",
            query.GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(root.GetProperty("required").EnumerateArray());
        Assert.Contains(
            "sem filtro",
            root.GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "corpo completo",
            root.GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "GmailReadMessage",
            definition.Description,
            StringComparison.Ordinal);
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains(
            "GmailSearch",
            definition.Description,
            StringComparison.Ordinal);

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

    [Theory]
    [InlineData(AgentToolKind.CalendarSearch, AgentToolRiskLevel.Read, "CalendarReadEvent", "maxResults")]
    [InlineData(AgentToolKind.CalendarReadEvent, AgentToolRiskLevel.Read, "CalendarSearch", "eventId")]
    [InlineData(AgentToolKind.CalendarCreateEvent, AgentToolRiskLevel.Sensitive, "aprovação humana", "summary")]
    [InlineData(AgentToolKind.CalendarUpdateEvent, AgentToolRiskLevel.Sensitive, "aprovação humana", "eventId")]
    [InlineData(AgentToolKind.CalendarDeleteEvent, AgentToolRiskLevel.Sensitive, "aprovação humana", "eventId")]
    public async Task GetAvailableToolsAsync_CalendarTools_ExposeStrictSafeContracts(
        AgentToolKind kind,
        AgentToolRiskLevel risk,
        string descriptionFragment,
        string requiredProperty)
    {
        await using ServiceProvider provider = CreateProvider();
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var agent = CreateAgent(tenantId);
        var tool = CreateGmailTool(tenantId, kind, Guid.NewGuid());
        tool.SetRiskLevel(risk);
        db.AddRange(agent, tool, new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();

        AgentToolDefinition definition = Assert.Single(await new AgentToolCatalog(db).GetAvailableToolsAsync(agent.Id));
        using JsonDocument schema = JsonDocument.Parse(definition.InputSchema!);
        Assert.Equal(kind, definition.Kind);
        Assert.Equal(risk, definition.RiskLevel);
        Assert.Contains(descriptionFragment, definition.Description, StringComparison.OrdinalIgnoreCase);
        Assert.True(schema.RootElement.GetProperty("properties").TryGetProperty(requiredProperty, out _));
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        string serialized = JsonSerializer.Serialize(definition);
        Assert.DoesNotContain("IntegrationConnectionId", serialized);
        Assert.DoesNotContain("accessToken", serialized, StringComparison.OrdinalIgnoreCase);
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

    [Theory]
    [InlineData(AgentToolKind.GmailCreateDraft, "to", "subject", "body")]
    [InlineData(AgentToolKind.GmailSend, "draftId", null, null)]
    [InlineData(AgentToolKind.GmailReply, "messageId", "body", null)]
    public async Task GetAvailableToolsAsync_GmailWriteOperations_UseMinimalDerivedSchemas(
        AgentToolKind kind,
        string firstRequired,
        string? secondRequired,
        string? thirdRequired)
    {
        await using ServiceProvider provider = CreateProvider();
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Guid tenantId = Guid.NewGuid();
        var agent = CreateAgent(tenantId);
        var tool = CreateGmailTool(tenantId, kind, Guid.NewGuid());
        tool.SetRiskLevel(GmailToolPolicy.RequiredRiskLevel(kind));
        db.AddRange(agent, tool, new AgentToolBinding(tenantId, agent.Id, tool.Id));
        await db.SaveChangesAsync();

        AgentToolDefinition definition = Assert.Single(
            await new AgentToolCatalog(db).GetAvailableToolsAsync(agent.Id));
        using JsonDocument schema = JsonDocument.Parse(definition.InputSchema!);
        JsonElement root = schema.RootElement;
        Assert.True(root.GetProperty("properties").TryGetProperty(firstRequired, out _));
        if (secondRequired is not null) Assert.True(root.GetProperty("properties").TryGetProperty(secondRequired, out _));
        if (thirdRequired is not null) Assert.True(root.GetProperty("properties").TryGetProperty(thirdRequired, out _));
        Assert.False(root.GetProperty("properties").TryGetProperty("accessToken", out _));
        Assert.False(root.GetProperty("properties").TryGetProperty("connectionId", out _));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
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
