using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;

namespace OrizonAgents.Application.Tools;

public interface IToolCredentialService
{
    Task<IReadOnlyList<ToolCredentialListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> CreateAsync(CreateToolCredentialRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> RotateSecretAsync(Guid credentialId, string secret, CancellationToken cancellationToken = default);
    Task<OperationResult> SetActiveAsync(Guid credentialId, bool active, CancellationToken cancellationToken = default);
    Task<ResolvedToolCredential?> ResolveForExecutionAsync(Guid credentialId, Guid tenantId, CancellationToken cancellationToken = default);
}
