using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;

namespace OrizonAgents.Application.Tools;

public interface IAgentToolService
{
    Task<IReadOnlyList<AgentToolListItemDto>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<AgentToolDetailsDto?> GetAsync(
        Guid toolId,
        CancellationToken cancellationToken = default);

    Task<OperationResult<Guid>> CreateAsync(
        CreateAgentToolRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateAsync(
        UpdateAgentToolRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationResult> ActivateAsync(
        Guid toolId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> DeactivateAsync(
        Guid toolId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentToolBindingDto>> ListForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> BindAsync(
        Guid agentId,
        Guid toolId,
        CancellationToken cancellationToken = default);

    Task<OperationResult> UnbindAsync(
        Guid agentId,
        Guid toolId,
        CancellationToken cancellationToken = default);
}
