namespace OrizonAgents.Infrastructure.Integrations.Google;

// Serialized only inside IntegrationConnectionCredentialProtector's protected envelope.
internal sealed class GoogleOAuthCredentials
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string Scope { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public override string ToString() => "[protected Google OAuth credentials]";
}