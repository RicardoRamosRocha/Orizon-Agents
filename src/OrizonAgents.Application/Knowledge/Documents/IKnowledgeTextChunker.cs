using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Application.Knowledge.Documents;

public interface IKnowledgeTextChunker
{
    IReadOnlyList<KnowledgeDocumentChunk> Chunk(
        string text);
}
