using OrizonAgents.Domain.Knowledge;

namespace OrizonAgents.Application.Knowledge.Models;

public sealed record KnowledgeBaseListItemDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int DocumentCount);

public sealed record KnowledgeBaseDetailsDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<KnowledgeDocumentDto> Documents);

public sealed record KnowledgeDocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    KnowledgeDocumentStatus Status,
    string? ProcessingError,
    int ChunkCount,
    DateTime CreatedAtUtc);

public sealed record AgentKnowledgeBindingDto(
    Guid KnowledgeBaseId,
    string KnowledgeBaseName,
    string? Description,
    bool IsActive,
    int DocumentCount,
    bool IsBound);
