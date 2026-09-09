namespace OrizonAgents.Application.Integrations.Google;

public enum GoogleOAuthCapability
{
    BasicIdentity = 1,
    GmailRead = 2,
    GmailCreateDraft = 3,
    GmailSend = 4,
    GmailReply = 5
}

// Server-side capability query. It never returns OAuth credentials or granted scopes.
public interface IGoogleOAuthCapabilityService
{
    Task<bool> HasCapabilityAsync(
        Guid connectionId,
        GoogleOAuthCapability capability,
        CancellationToken cancellationToken = default);
}
