using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Integrations.Google;

public interface IGoogleOAuthService
{
    Task<OperationResult<string>> BeginAsync(Guid connectionId, string redirectUri, string correlation, CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> CompleteAsync(string? state, string? code, string? error, string? correlation, CancellationToken cancellationToken = default);
    // False means local credentials were removed but remote revocation could not be confirmed.
    Task<OperationResult<bool>> DisconnectAsync(Guid connectionId, CancellationToken cancellationToken = default);
}