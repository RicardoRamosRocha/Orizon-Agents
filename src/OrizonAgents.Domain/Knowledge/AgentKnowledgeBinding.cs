using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Knowledge;

public sealed class AgentKnowledgeBinding : AuditableEntity, ITenantOwnedEntity
{
    private AgentKnowledgeBinding()
    {
    }

    public AgentKnowledgeBinding(
        Guid tenantId,
        Guid agentId,
        Guid knowledgeBaseId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent id is required.", nameof(agentId));

        if (knowledgeBaseId == Guid.Empty)
            throw new ArgumentException("Knowledge base id is required.", nameof(knowledgeBaseId));

        TenantId = tenantId;
        AgentId = agentId;
        KnowledgeBaseId = knowledgeBaseId;
    }

    public Guid TenantId { get; private set; }

    public Guid AgentId { get; private set; }

    public Guid KnowledgeBaseId { get; private set; }

    public AiAgent Agent { get; private set; } = null!;

    public KnowledgeBase KnowledgeBase { get; private set; } = null!;
}
