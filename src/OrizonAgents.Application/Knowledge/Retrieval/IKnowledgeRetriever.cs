using OrizonAgents.Application.Knowledge.Retrieval.Models;

namespace OrizonAgents.Application.Knowledge.Retrieval;

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
        Guid agentId,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}
