using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Knowledge;

public sealed class KnowledgeDocument : AuditableEntity, ITenantOwnedEntity
{
    private KnowledgeDocument()
    {
    }

    public KnowledgeDocument(
        Guid tenantId,
        Guid knowledgeBaseId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        if (knowledgeBaseId == Guid.Empty)
            throw new ArgumentException("Knowledge base id is required.", nameof(knowledgeBaseId));

        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        TenantId = tenantId;
        KnowledgeBaseId = knowledgeBaseId;
        FileName = Required(fileName, nameof(fileName));
        ContentType = Required(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        StorageKey = Required(storageKey, nameof(storageKey));
        Status = KnowledgeDocumentStatus.Pending;
    }

    public Guid TenantId { get; private set; }

    public Guid KnowledgeBaseId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    public KnowledgeDocumentStatus Status { get; private set; }

    public string? ProcessingError { get; private set; }

    public KnowledgeBase KnowledgeBase { get; private set; } = null!;

    public ICollection<KnowledgeChunk> Chunks { get; private set; } =
        new List<KnowledgeChunk>();

    public void MarkProcessing()
    {
        Status = KnowledgeDocumentStatus.Processing;
        ProcessingError = null;
    }

    public void MarkReady()
    {
        Status = KnowledgeDocumentStatus.Ready;
        ProcessingError = null;
    }

    public void MarkFailed(string error)
    {
        Status = KnowledgeDocumentStatus.Failed;
        ProcessingError = string.IsNullOrWhiteSpace(error)
            ? "Document processing failed."
            : error.Trim();
    }

    private static string Required(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(
                "Value is required.",
                parameterName);
        }

        return normalized;
    }
}
