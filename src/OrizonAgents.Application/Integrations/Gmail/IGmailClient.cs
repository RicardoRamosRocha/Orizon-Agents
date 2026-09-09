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

    Task<GmailDraft> CreateDraftAsync(
        Guid connectionId,
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default);

    Task<GmailSentMessage> SendDraftAsync(
        Guid connectionId,
        string draftId,
        CancellationToken cancellationToken = default);
}

public sealed record GmailSearchResult(
    IReadOnlyList<GmailMessageReference> Messages,
    string? NextPageToken,
    long? ResultSizeEstimate);

public sealed record GmailMessageReference(
    string Id,
    string ThreadId,
    string? Subject = null,
    string? From = null);

public sealed record GmailMessage(
    string Id,
    string ThreadId,
    string? Subject,
    string? From,
    string? To,
    DateTimeOffset? Date,
    string? Snippet,
    string? BodyText);

public sealed record GmailDraft(
    string DraftId,
    string? MessageId,
    string? ThreadId);

public sealed record GmailSentMessage(
    string MessageId,
    string? ThreadId);
