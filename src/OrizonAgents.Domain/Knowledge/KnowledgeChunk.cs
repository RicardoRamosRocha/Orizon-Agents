using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Knowledge;

public sealed class KnowledgeChunk : AuditableEntity, ITenantOwnedEntity
{
    private KnowledgeChunk()
    {
    }

    public KnowledgeChunk(
        Guid tenantId,
        Guid documentId,
        int position,
        string content)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        if (documentId == Guid.Empty)
            throw new ArgumentException("Document id is required.", nameof(documentId));

        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required.", nameof(content));

        TenantId = tenantId;
        DocumentId = documentId;
        Position = position;
        Content = content.Trim();
    }

    public Guid TenantId { get; private set; }

    public Guid DocumentId { get; private set; }

    public int Position { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public KnowledgeDocument Document { get; private set; } = null!;
}
