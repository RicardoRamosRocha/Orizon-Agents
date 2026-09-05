using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Infrastructure.Integrations.Google;

internal static class GoogleOAuthScopeCatalog
{
    internal const string OpenId = "openid";
    internal const string Email = "email";
    internal const string GmailReadOnly = "https://www.googleapis.com/auth/gmail.readonly";
    internal const string BasicIdentityRequest = OpenId + " " + Email;

    internal static bool IsUpgradeCapability(GoogleOAuthCapability capability) =>
        capability == GoogleOAuthCapability.GmailRead;

    internal static string AuthorizationScopes(GoogleOAuthCapability? capability) => capability switch
    {
        null => BasicIdentityRequest,
        GoogleOAuthCapability.GmailRead => BasicIdentityRequest + " " + GmailReadOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), "Capability Google OAuth inválida.")
    };

    internal static bool HasCapability(string? grantedScopes, GoogleOAuthCapability capability)
    {
        HashSet<string> scopes = Parse(grantedScopes);
        return capability switch
        {
            GoogleOAuthCapability.BasicIdentity => scopes.Contains(OpenId) && scopes.Contains(Email),
            GoogleOAuthCapability.GmailRead => scopes.Contains(GmailReadOnly),
            _ => false
        };
    }

    internal static string Normalize(string? grantedScopes) =>
        string.Join(' ', Parse(grantedScopes).Order(StringComparer.Ordinal));

    private static HashSet<string> Parse(string? grantedScopes)
    {
        if (string.IsNullOrWhiteSpace(grantedScopes))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return grantedScopes
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }
}
