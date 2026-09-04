using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Application.Integrations.Requests;

namespace OrizonAgents.Application.Integrations;

public interface IIntegrationConnectionService
{
    Task<IReadOnlyList<IntegrationConnectionDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<IntegrationConnectionDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> CreateAsync(CreateIntegrationConnectionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(Guid id, UpdateIntegrationConnectionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}