using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OrizonAgents.Application.Integrations.Calendar;
using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Infrastructure.Integrations.Calendar;

public sealed class CalendarClient(IHttpClientFactory clients, IGoogleOAuthTokenService tokens) : ICalendarClient
{
    public const string HttpClientName = "GoogleCalendar";
    private const string BaseUrl = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    public async Task<CalendarSearchResult> SearchEventsAsync(CalendarSearchRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?> { ["maxResults"] = request.MaxResults.ToString(CultureInfo.InvariantCulture), ["singleEvents"] = "true", ["orderBy"] = "startTime" };
        if (!string.IsNullOrWhiteSpace(request.Query)) parameters["q"] = request.Query.Trim();
        if (request.TimeMin.HasValue) parameters["timeMin"] = request.TimeMin.Value.ToString("O");
        if (request.TimeMax.HasValue) parameters["timeMax"] = request.TimeMax.Value.ToString("O");
        using JsonDocument json = await SendAsync(HttpMethod.Get, QueryHelpers.AddQueryString(BaseUrl, parameters), null, request.ConnectionId, cancellationToken);
        var events = json.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Select(ParseReference).ToArray() : [];
        return new CalendarSearchResult(events, ReadString(json.RootElement, "nextPageToken"));
    }

    public async Task<CalendarEvent> GetEventAsync(Guid connectionId, string eventId, CancellationToken cancellationToken = default) =>
        ParseEvent(await SendAsync(HttpMethod.Get, EventUrl(eventId), null, connectionId, cancellationToken));

    public async Task<CalendarEvent> CreateEventAsync(CalendarEventMutation request, CancellationToken cancellationToken = default) =>
        ParseEvent(await SendAsync(HttpMethod.Post, BaseUrl + "?sendUpdates=none", SerializeMutation(request), request.ConnectionId, cancellationToken));

    public async Task<CalendarEvent> UpdateEventAsync(string eventId, CalendarEventMutation request, CancellationToken cancellationToken = default) =>
        ParseEvent(await SendAsync(HttpMethod.Patch, EventUrl(eventId) + "?sendUpdates=none", SerializeMutation(request), request.ConnectionId, cancellationToken));

    public async Task DeleteEventAsync(Guid connectionId, string eventId, CancellationToken cancellationToken = default)
    {
        using JsonDocument _ = await SendAsync(HttpMethod.Delete, EventUrl(eventId) + "?sendUpdates=none", null, connectionId, cancellationToken);
    }

    // ConnectionId is resolved only by the executor; read/delete receive it through the request URI context-free overload below.
    private async Task<JsonDocument> SendAsync(HttpMethod method, string url, object? body, Guid connectionId, CancellationToken ct)
    {
        if (connectionId == Guid.Empty) throw new ArgumentException("ConnectionId is required.", nameof(connectionId));
        var access = await tokens.GetAccessTokenAsync(connectionId, ct);
        if (!access.Succeeded || access.Value is null) throw new InvalidOperationException(access.FirstError ?? "Google authorization unavailable.");
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Value.Value);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var client = clients.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new CalendarApiException(response.StatusCode);
        string content = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(content) ? "{}" : content);
    }

    private static string EventUrl(string eventId) => string.IsNullOrWhiteSpace(eventId) ? throw new ArgumentException("EventId is required.", nameof(eventId)) : BaseUrl + "/" + Uri.EscapeDataString(eventId.Trim());
    private static object SerializeMutation(CalendarEventMutation x) => new { summary = x.Summary, start = new { dateTime = x.Start.ToString("O"), timeZone = x.TimeZone }, end = new { dateTime = x.End.ToString("O"), timeZone = x.TimeZone }, description = x.Description, location = x.Location, attendees = x.Attendees?.Select(email => new { email }) };
    private static CalendarEventReference ParseReference(JsonElement x) { CalendarEvent e = Parse(x); return new(e.Id, e.Summary, e.Start, e.End, e.TimeZone, e.Organizer, e.Attendees, e.Status, e.HtmlLink); }
    private static CalendarEvent ParseEvent(JsonDocument x) => Parse(x.RootElement);
    private static CalendarEvent Parse(JsonElement x) => new(ReadString(x,"id") ?? string.Empty, ReadString(x,"summary"), ParseDate(x,"start"), ParseDate(x,"end"), ReadString(x,"timeZone"), x.TryGetProperty("organizer",out var o) ? ReadString(o,"email") : null, x.TryGetProperty("attendees",out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(y=>ReadString(y,"email")).Where(y=>y is not null).Cast<string>().Take(20).ToArray() : [], ReadString(x,"description"), ReadString(x,"location"), ReadString(x,"status"), ReadString(x,"htmlLink"));
    private static CalendarDateTime ParseDate(JsonElement x, string name) { if (!x.TryGetProperty(name,out var v)) return new(null,null); return DateTimeOffset.TryParse(ReadString(v,"dateTime"), out var dt) ? new(dt,null) : DateOnly.TryParse(ReadString(v,"date"), out var date) ? new(null,date) : new(null,null); }
    private static string? ReadString(JsonElement x, string name) => x.TryGetProperty(name,out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
public sealed class CalendarApiException(System.Net.HttpStatusCode statusCode) : Exception("Calendar API request failed.") { public System.Net.HttpStatusCode StatusCode { get; } = statusCode; }
