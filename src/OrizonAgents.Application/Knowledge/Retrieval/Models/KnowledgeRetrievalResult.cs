namespace OrizonAgents.Application.Knowledge.Retrieval.Models;

public sealed record KnowledgeRetrievalResult(
    Guid KnowledgeBaseId,
    string KnowledgeBaseName,
    Guid DocumentId,
    string DocumentName,
    int ChunkPosition,
    string Content,
    double? SemanticScore = null,
    double? LexicalScore = null,
    double? HybridScore = null,
    Guid? KnowledgeChunkId = null);
