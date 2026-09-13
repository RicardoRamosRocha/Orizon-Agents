using System.Text.Json.Serialization;
using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Integrations.Google;

public interface IGoogleOAuthTokenService
{
    Task<OperationResult<GoogleAccessToken>> GetAccessTokenAsync(Guid connectionId, CancellationToken cancellationToken = default);
}

// Internal execution result, never an administrative DTO. Accidental JSON/log output is redacted.
public sealed class GoogleAccessToken(string value)
{
    [JsonIgnore]
    public string Value { get; } = value;
    public override string ToString() => "[protected Google access token]";
}