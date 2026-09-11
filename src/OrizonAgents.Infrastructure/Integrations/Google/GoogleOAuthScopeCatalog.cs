using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Infrastructure.Integrations.Google;

internal static class GoogleOAuthScopeCatalog
{
    internal const string OpenId = "openid";
    internal const string Email = "email";
    internal const string GmailReadOnly = "https://www.googleapis.com/auth/gmail.readonly";
    internal const string GmailCompose = "https://www.googleapis.com/auth/gmail.compose";
    internal const string GmailSend = "https://www.googleapis.com/auth/gmail.send";
    internal const string CalendarRead = "https://www.googleapis.com/auth/calendar.events.readonly";
    internal const string CalendarWrite = "https://www.googleapis.com/auth/calendar.events";
    internal const string BasicIdentityRequest = OpenId + " " + Email;

    internal static bool IsUpgradeCapability(GoogleOAuthCapability capability) =>
        capability is GoogleOAuthCapability.GmailRead or
            GoogleOAuthCapability.GmailCreateDraft or
            GoogleOAuthCapability.GmailSend or
            GoogleOAuthCapability.GmailReply or GoogleOAuthCapability.CalendarRead or GoogleOAuthCapability.CalendarWrite;

    internal static string AuthorizationScopes(GoogleOAuthCapability? capability) => capability switch
    {
        null => BasicIdentityRequest,
        GoogleOAuthCapability.GmailRead => BasicIdentityRequest + " " + GmailReadOnly,
        GoogleOAuthCapability.GmailCreateDraft => BasicIdentityRequest + " " + GmailCompose,
        GoogleOAuthCapability.GmailSend => BasicIdentityRequest + " " + GmailCompose,
        GoogleOAuthCapability.GmailReply => BasicIdentityRequest + " " + GmailReadOnly + " " + GmailSend,
        GoogleOAuthCapability.CalendarRead => BasicIdentityRequest + " " + CalendarRead,
        GoogleOAuthCapability.CalendarWrite => BasicIdentityRequest + " " + CalendarWrite,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), "Capability Google OAuth inválida.")
    };

    internal static bool HasCapability(string? grantedScopes, GoogleOAuthCapability capability)
    {
        HashSet<string> scopes = Parse(grantedScopes);
        return capability switch
        {
            GoogleOAuthCapability.BasicIdentity => scopes.Contains(OpenId) && scopes.Contains(Email),
            GoogleOAuthCapability.GmailRead => scopes.Contains(GmailReadOnly),
            GoogleOAuthCapability.GmailCreateDraft => scopes.Contains(GmailCompose),
            GoogleOAuthCapability.GmailSend => scopes.Contains(GmailCompose),
            GoogleOAuthCapability.GmailReply =>
                scopes.Contains(GmailReadOnly) && scopes.Contains(GmailSend),
            GoogleOAuthCapability.CalendarRead => scopes.Contains(CalendarRead) || scopes.Contains(CalendarWrite),
            GoogleOAuthCapability.CalendarWrite => scopes.Contains(CalendarWrite),
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
