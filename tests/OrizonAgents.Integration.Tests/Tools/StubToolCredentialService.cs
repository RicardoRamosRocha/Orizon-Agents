using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Application.Tools.Requests;

namespace OrizonAgents.Integration.Tests.Tools;

internal sealed class StubToolCredentialService : IToolCredentialService
{
    public ResolvedToolCredential? ResolvedCredential { get; set; }
    public int ResolveCalls { get; private set; }

    public Task<ResolvedToolCredential?> ResolveForExecutionAsync(
        Guid credentialId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return Task.FromResult(ResolvedCredential);
    }

    public Task<IReadOnlyList<ToolCredentialListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ToolCredentialListItemDto>>(Array.Empty<ToolCredentialListItemDto>());

    public Task<OperationResult<Guid>> CreateAsync(CreateToolCredentialRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<OperationResult> RotateSecretAsync(Guid credentialId, string secret, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<OperationResult> SetActiveAsync(Guid credentialId, bool active, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
