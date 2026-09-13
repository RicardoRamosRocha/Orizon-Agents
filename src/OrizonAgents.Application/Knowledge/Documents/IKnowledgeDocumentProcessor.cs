using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Knowledge.Documents;

public interface IKnowledgeDocumentProcessor
{
    Task<OperationResult> ProcessAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
