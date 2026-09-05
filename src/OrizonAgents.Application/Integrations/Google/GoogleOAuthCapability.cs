namespace OrizonAgents.Application.Integrations.Google;

public enum GoogleOAuthCapability
{
    BasicIdentity = 1,
    GmailRead = 2
}

// Server-side capability query. It never returns OAuth credentials or granted scopes.
public interface IGoogleOAuthCapabilityService
{
    Task<bool> HasCapabilityAsync(
        Guid connectionId,
        GoogleOAuthCapability capability,
        CancellationToken cancellationToken = default);
}
