using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Agents.Requests;

namespace OrizonAgents.Application.Agents;

public interface IAiAgentService
{
    Task<IReadOnlyList<AiAgentListItemDto>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<AiAgentDetailsDto?> GetAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(
        CreateAiAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        UpdateAiAgentRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ActivateAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeactivateAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}
