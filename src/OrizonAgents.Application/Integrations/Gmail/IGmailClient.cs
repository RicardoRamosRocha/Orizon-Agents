namespace OrizonAgents.Application.Integrations.Gmail;

public interface IGmailClient
{
    Task<GmailSearchResult> SearchMessagesAsync(
        Guid connectionId,
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    Task<GmailMessage> GetMessageAsync(
        Guid connectionId,
        string messageId,
        CancellationToken cancellationToken = default);
}

public sealed record GmailSearchResult(
    IReadOnlyList<GmailMessageReference> Messages,
    string? NextPageToken,
    long? ResultSizeEstimate);

public sealed record GmailMessageReference(
    string Id,
    string ThreadId);

public sealed record GmailMessage(
    string Id,
    string ThreadId,
    string? Subject,
    string? From,
    string? To,
    DateTimeOffset? Date,
    string? Snippet,
    string? BodyText);