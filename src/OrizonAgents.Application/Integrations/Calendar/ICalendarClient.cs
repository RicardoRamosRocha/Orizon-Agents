namespace OrizonAgents.Application.Integrations.Calendar;

public interface ICalendarClient
{
    Task<CalendarSearchResult> SearchEventsAsync(CalendarSearchRequest request, CancellationToken cancellationToken = default);
    Task<CalendarEvent> GetEventAsync(Guid connectionId, string eventId, CancellationToken cancellationToken = default);
    Task<CalendarEvent> CreateEventAsync(CalendarEventMutation request, CancellationToken cancellationToken = default);
    Task<CalendarEvent> UpdateEventAsync(string eventId, CalendarEventMutation request, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid connectionId, string eventId, CancellationToken cancellationToken = default);
}

public sealed record CalendarSearchRequest(Guid ConnectionId, string? Query, DateTimeOffset? TimeMin, DateTimeOffset? TimeMax, int MaxResults);
public sealed record CalendarEventMutation(Guid ConnectionId, string Summary, DateTimeOffset Start, DateTimeOffset End, string? TimeZone = null, string? Description = null, string? Location = null, IReadOnlyList<string>? Attendees = null);
public sealed record CalendarSearchResult(IReadOnlyList<CalendarEventReference> Events, string? NextPageToken);
public sealed record CalendarEventReference(string Id, string? Summary, CalendarDateTime Start, CalendarDateTime End, string? TimeZone, string? Organizer, IReadOnlyList<string> Attendees, string? Status, string? HtmlLink);
public sealed record CalendarEvent(string Id, string? Summary, CalendarDateTime Start, CalendarDateTime End, string? TimeZone, string? Organizer, IReadOnlyList<string> Attendees, string? Description, string? Location, string? Status, string? HtmlLink);
public sealed record CalendarDateTime(DateTimeOffset? DateTime, DateOnly? Date);
