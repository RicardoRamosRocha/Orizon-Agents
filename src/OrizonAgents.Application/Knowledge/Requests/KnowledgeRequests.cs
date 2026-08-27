namespace OrizonAgents.Application.Knowledge.Requests;

public sealed record CreateKnowledgeBaseRequest(
    string Name,
    string? Description);

public sealed record UploadKnowledgeDocumentRequest(
    Guid KnowledgeBaseId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);
