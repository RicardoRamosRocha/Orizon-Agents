using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Application.Knowledge.Documents;

public interface IKnowledgeDocumentExtractor
{
    bool CanExtract(
        string fileName,
        string contentType);

    Task<KnowledgeDocumentContent> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}
