using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Infrastructure.Integrations.Google;

public sealed class GoogleOAuthStateProtector(IDataProtectionProvider provider, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly IDataProtector _protector = provider.CreateProtector("OrizonAgents.GoogleOAuth.State.v1");

    public string Protect(
        Guid tenantId,
        Guid connectionId,
        Guid userId,
        string redirectUri,
        string correlation,
        string verifier,
        GoogleOAuthCapability? capability = null) =>
        _protector.Protect(JsonSerializer.Serialize(new GoogleOAuthState
        {
            TenantId = tenantId,
            ConnectionId = connectionId,
            UserId = userId,
            RedirectUri = redirectUri,
            CorrelationHash = Hash(correlation),
            CodeVerifier = verifier,
            ExpiresAtUtc = clock.GetUtcNow().Add(Lifetime),
            Capability = capability
        }));

    internal GoogleOAuthState? Unprotect(string? state)
    {
        if (string.IsNullOrWhiteSpace(state) || state.Length > 8192)
        {
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<GoogleOAuthState>(_protector.Unprotect(state));
            return value is not null && value.ExpiresAtUtc > clock.GetUtcNow()
                && value.TenantId != Guid.Empty && value.ConnectionId != Guid.Empty && value.UserId != Guid.Empty
                && !string.IsNullOrWhiteSpace(value.CodeVerifier)
                && (!value.Capability.HasValue || GoogleOAuthScopeCatalog.IsUpgradeCapability(value.Capability.Value))
                ? value : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string NewVerifier() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Challenge(string verifier) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
}

internal sealed class GoogleOAuthState
{
    public Guid TenantId { get; init; }
    public Guid ConnectionId { get; init; }
    public Guid UserId { get; init; }
    public string RedirectUri { get; init; } = string.Empty;
    public string CorrelationHash { get; init; } = string.Empty;
    public string CodeVerifier { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
    public GoogleOAuthCapability? Capability { get; init; }
    public override string ToString() => "[protected Google OAuth state]";
}
