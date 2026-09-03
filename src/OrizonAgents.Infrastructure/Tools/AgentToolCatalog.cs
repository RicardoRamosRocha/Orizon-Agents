using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools;

public sealed class AgentToolCatalog : IAgentToolCatalog
{
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

        return await (
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
            select new AgentToolDefinition(
                tool.Id,
                tool.Name,
                tool.Description,
                tool.HttpMethod,
                tool.InputSchema,
                tool.RiskLevel))
            .ToListAsync(cancellationToken);
    }
}
