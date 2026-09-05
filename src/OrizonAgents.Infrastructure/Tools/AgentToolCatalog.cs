using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools;

public sealed class AgentToolCatalog : IAgentToolCatalog
{
    private const string GmailSearchInputSchema = """
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "Consulta de pesquisa do Gmail."
            },
            "maxResults": {
              "type": "integer",
              "minimum": 1,
              "maximum": 100,
              "description": "Quantidade máxima de resultados."
            }
          },
          "required": ["query"],
          "additionalProperties": false
        }
        """;

    private const string GmailReadMessageInputSchema = """
        {
          "type": "object",
          "properties": {
            "messageId": {
              "type": "string",
              "description": "Identificador da mensagem Gmail."
            }
          },
          "required": ["messageId"],
          "additionalProperties": false
        }
        """;

    private readonly OrizonAgentsDbContext _dbContext;

    public AgentToolCatalog(
        OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AgentToolDefinition>> GetAvailableToolsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            return Array.Empty<AgentToolDefinition>();
        }

        List<CatalogToolProjection> tools = await (
            from binding in _dbContext.AgentToolBindings.AsNoTracking()
            join tool in _dbContext.AgentTools.AsNoTracking()
                on binding.ToolId equals tool.Id
            join agent in _dbContext.AiAgents.AsNoTracking()
                on binding.AgentId equals agent.Id
            where binding.AgentId == agentId
                  && binding.TenantId == agent.TenantId
                  && tool.TenantId == agent.TenantId
                  && binding.IsActive
                  && tool.IsActive
            orderby tool.Name
            select new CatalogToolProjection(
                tool.Id,
                tool.Name,
                tool.Description,
                tool.HttpMethod,
                tool.InputSchema,
                tool.RiskLevel,
                tool.Kind))
            .ToListAsync(cancellationToken);

        return tools
            .Select(tool =>
                new AgentToolDefinition(
                    tool.Id,
                    tool.Name,
                    tool.Description,
                    tool.HttpMethod,
                    ResolveInputSchema(
                        tool.Kind,
                        tool.InputSchema),
                    tool.RiskLevel,
                    tool.Kind))
            .ToList();
    }

    private static string? ResolveInputSchema(
        AgentToolKind kind,
        string? configuredSchema)
    {
        if (!string.IsNullOrWhiteSpace(configuredSchema))
        {
            return configuredSchema;
        }

        return kind switch
        {
            AgentToolKind.GmailSearch =>
                GmailSearchInputSchema,

            AgentToolKind.GmailReadMessage =>
                GmailReadMessageInputSchema,

            _ => null
        };
    }

    private sealed record CatalogToolProjection(
        Guid Id,
        string Name,
        string Description,
        string HttpMethod,
        string? InputSchema,
        AgentToolRiskLevel RiskLevel,
        AgentToolKind Kind);
}
