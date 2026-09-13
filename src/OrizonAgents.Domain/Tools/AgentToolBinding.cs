using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tools;

public sealed class AgentToolBinding : AuditableEntity, ITenantOwnedEntity
{
    private AgentToolBinding()
    {
    }

    public AgentToolBinding(
        Guid tenantId,
        Guid agentId,
        Guid toolId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "TenantId é obrigatório.",
                nameof(tenantId));
        }

        if (agentId == Guid.Empty)
        {
            throw new ArgumentException(
                "AgentId é obrigatório.",
                nameof(agentId));
        }

        if (toolId == Guid.Empty)
        {
            throw new ArgumentException(
                "ToolId é obrigatório.",
                nameof(toolId));
        }

        TenantId = tenantId;
        AgentId = agentId;
        ToolId = toolId;
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public Guid AgentId { get; private set; }

    public Guid ToolId { get; private set; }

    public bool IsActive { get; private set; }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
