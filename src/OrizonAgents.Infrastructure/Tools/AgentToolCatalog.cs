using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools;

public sealed class AgentToolCatalog : IAgentToolCatalog
{
    private const string CalendarSearchInputSchema = """{"type":"object","properties":{"query":{"type":"string"},"timeMin":{"type":"string","format":"date-time"},"timeMax":{"type":"string","format":"date-time"},"maxResults":{"type":"integer","minimum":1,"maximum":20}},"additionalProperties":false}""";
    private const string CalendarReadInputSchema = """{"type":"object","properties":{"eventId":{"type":"string","minLength":1}},"required":["eventId"],"additionalProperties":false}""";
    private const string CalendarCreateInputSchema = """{"type":"object","properties":{"summary":{"type":"string","minLength":1},"start":{"type":"string","format":"date-time"},"end":{"type":"string","format":"date-time"},"timeZone":{"type":"string"},"description":{"type":"string"},"location":{"type":"string"},"attendees":{"type":"array","items":{"type":"string","format":"email"},"maxItems":20}},"required":["summary","start","end"],"additionalProperties":false}""";
    private const string CalendarUpdateInputSchema = """{"type":"object","properties":{"eventId":{"type":"string","minLength":1},"summary":{"type":"string","minLength":1},"start":{"type":"string","format":"date-time"},"end":{"type":"string","format":"date-time"},"timeZone":{"type":"string"},"description":{"type":"string"},"location":{"type":"string"},"attendees":{"type":"array","items":{"type":"string","format":"email"},"maxItems":20}},"required":["eventId","summary","start","end"],"additionalProperties":false}""";
    private const string GmailSearchInputSchema = """
        {
          "type": "object",
          "description": "Pesquise primeiro para localizar mensagens candidatas. Sem query, busca mensagens sem filtro. O resultado contém somente metadados compactos, sem corpo completo. Depois escolha uma mensagem relevante e use GmailReadMessage com o messageId; não leia mensagens desnecessárias.",
          "properties": {
            "query": {
              "type": "string",
              "description": "Filtro opcional de pesquisa do Gmail. Omita para buscar mensagens sem filtro."
            },
            "maxResults": {
              "type": "integer",
              "minimum": 1,
              "maximum": 20,
              "description": "Quantidade máxima de resultados compactos para seleção."
            }
          },
          "required": [],
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

    private const string GmailCreateDraftInputSchema = """
        {
          "type": "object",
          "properties": {
            "to": { "type": "string", "format": "email" },
            "subject": { "type": "string", "minLength": 1 },
            "body": { "type": "string", "minLength": 1 }
          },
          "required": ["to", "subject", "body"],
          "additionalProperties": false
        }
        """;

    private const string GmailSendInputSchema = """
        {
          "type": "object",
          "properties": {
            "draftId": { "type": "string", "minLength": 1 }
          },
          "required": ["draftId"],
          "additionalProperties": false
        }
        """;

    private const string GmailReplyInputSchema = """
        {
          "type": "object",
          "properties": {
            "messageId": { "type": "string", "minLength": 1 },
            "body": { "type": "string", "minLength": 1 }
          },
          "required": ["messageId", "body"],
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
                    ResolveDescription(tool.Kind, tool.Description),
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

            AgentToolKind.GmailCreateDraft => GmailCreateDraftInputSchema,
            AgentToolKind.GmailSend => GmailSendInputSchema,
            AgentToolKind.GmailReply => GmailReplyInputSchema,
            AgentToolKind.CalendarSearch => CalendarSearchInputSchema,
            AgentToolKind.CalendarReadEvent or AgentToolKind.CalendarDeleteEvent => CalendarReadInputSchema,
            AgentToolKind.CalendarCreateEvent => CalendarCreateInputSchema,
            AgentToolKind.CalendarUpdateEvent => CalendarUpdateInputSchema,

            _ => null
        };
    }

    private static string ResolveDescription(
        AgentToolKind kind,
        string description) => kind switch
    {
        AgentToolKind.GmailSearch =>
            description + " Use GmailSearch primeiro para localizar mensagens candidatas; " +
            "GmailSearch retorna apenas metadados compactos, sem o corpo. Depois use " +
            "GmailReadMessage com o messageId escolhido.",
        AgentToolKind.GmailReadMessage =>
            description + " Use somente após GmailSearch identificar uma mensagem " +
            "necessária, passando o messageId retornado pela busca.",
        AgentToolKind.CalendarSearch => description + " Use primeiro para localizar eventos e CalendarReadEvent para detalhes.",
        AgentToolKind.CalendarReadEvent => description + " Use com eventId após CalendarSearch quando detalhes forem necessários.",
        AgentToolKind.CalendarCreateEvent or AgentToolKind.CalendarUpdateEvent or AgentToolKind.CalendarDeleteEvent => description + " Esta ação exige aprovação humana antes da execução.",
        _ => description
    };

    private sealed record CatalogToolProjection(
        Guid Id,
        string Name,
        string Description,
        string HttpMethod,
        string? InputSchema,
        AgentToolRiskLevel RiskLevel,
        AgentToolKind Kind);
}
