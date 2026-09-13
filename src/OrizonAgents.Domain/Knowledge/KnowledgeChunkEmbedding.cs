using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Knowledge;

public sealed class KnowledgeChunkEmbedding : AuditableEntity, ITenantOwnedEntity
{
    private KnowledgeChunkEmbedding()
    {
        Embedding = Array.Empty<float>();
    }

    public KnowledgeChunkEmbedding(
        Guid tenantId,
        Guid knowledgeChunkId,
        string provider,
        string model,
        IReadOnlyCollection<float> embedding)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        if (knowledgeChunkId == Guid.Empty)
            throw new ArgumentException("Knowledge chunk id is required.", nameof(knowledgeChunkId));

        Provider = Required(provider, nameof(provider), 80);
        Model = Required(model, nameof(model), 160);

        if (embedding is null || embedding.Count == 0)
            throw new ArgumentException("Embedding is required.", nameof(embedding));

        if (embedding.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
            throw new ArgumentException("Embedding must contain finite values.", nameof(embedding));

        TenantId = tenantId;
        KnowledgeChunkId = knowledgeChunkId;
        Embedding = embedding.ToArray();
        Dimensions = Embedding.Length;
    }

    public Guid TenantId { get; private set; }

    public Guid KnowledgeChunkId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public int Dimensions { get; private set; }

    public float[] Embedding { get; private set; }

    public KnowledgeChunk KnowledgeChunk { get; private set; } = null!;

    private static string Required(string value, string parameterName, int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Value is required.", parameterName);

        if (normalized.Length > maxLength)
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);

        return normalized;
    }
}
