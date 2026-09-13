using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Knowledge.Models;
using OrizonAgents.Application.Knowledge.Requests;

namespace OrizonAgents.Application.Knowledge;

public interface IKnowledgeService
{
    Task<IReadOnlyList<KnowledgeBaseListItemDto>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<KnowledgeBaseDetailsDto?> GetAsync(
        Guid knowledgeBaseId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(
        CreateKnowledgeBaseRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> UploadDocumentAsync(
        UploadKnowledgeDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ProcessDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);


    Task<IReadOnlyList<AgentKnowledgeBindingDto>> ListForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> BindToAgentAsync(
        Guid agentId,
        Guid knowledgeBaseId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UnbindFromAgentAsync(
        Guid agentId,
        Guid knowledgeBaseId,
        CancellationToken cancellationToken = default);
}
