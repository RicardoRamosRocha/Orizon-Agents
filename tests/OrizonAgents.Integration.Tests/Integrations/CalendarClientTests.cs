using System.Net;
using System.Net.Http.Headers;
using System.Text;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Integrations.Calendar;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Infrastructure.Integrations.Calendar;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class CalendarClientTests
{
    [Fact]
    public async Task SearchEventsAsync_UsesPrimaryCalendarBearerAndCompactResults()
    {
        var handler = new RecordingHandler("""
            {"items":[{"id":"event-1","summary":"Reunião","start":{"date":"2026-09-11"},"end":{"date":"2026-09-12"},"timeZone":"America/Sao_Paulo","organizer":{"email":"owner@example.com"},"attendees":[{"email":"a@example.com"}],"description":"private","location":"Sala","status":"confirmed","htmlLink":"https://calendar.google/event-1"}]}
            """);
        var client = CreateClient(handler);

        CalendarSearchResult result = await client.SearchEventsAsync(new(
            Guid.NewGuid(), "cliente", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero), 10));

        CalendarEventReference item = Assert.Single(result.Events);
        Assert.Equal("event-1", item.Id);
        Assert.Equal("Reunião", item.Summary);
        Assert.Equal(new DateOnly(2026, 9, 11), item.Start.Date);
        Assert.Null(item.Start.DateTime);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("calendar-test-token", handler.Authorization?.Parameter);
        Assert.Equal("/calendar/v3/calendars/primary/events", handler.Uri!.AbsolutePath);
        Assert.Contains("q=cliente", handler.Uri.Query);
        Assert.Contains("timeMin=2026-09-01T00%3A00%3A00.0000000%2B00%3A00", handler.Uri.Query);
        Assert.Contains("timeMax=2026-09-30T00%3A00%3A00.0000000%2B00%3A00", handler.Uri.Query);
        Assert.Contains("maxResults=10", handler.Uri.Query);
        Assert.DoesNotContain("description", System.Text.Json.JsonSerializer.Serialize(item), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCreateUpdateAndDelete_UseExpectedMethodsSafeUpdatesAndParseDateTime()
    {
        var handler = new RecordingHandler(
            """{"id":"one","start":{"dateTime":"2026-09-10T09:00:00-03:00"},"end":{"dateTime":"2026-09-10T10:00:00-03:00"},"description":"details"}""",
            """{"id":"two","start":{"dateTime":"2026-09-10T09:00:00-03:00"},"end":{"dateTime":"2026-09-10T10:00:00-03:00"}}""",
            """{"id":"three","start":{"dateTime":"2026-09-10T09:00:00-03:00"},"end":{"dateTime":"2026-09-10T10:00:00-03:00"}}""",
            "");
        var client = CreateClient(handler);
        Guid connectionId = Guid.NewGuid();
        var mutation = new CalendarEventMutation(connectionId, "Evento", DateTimeOffset.Parse("2026-09-10T09:00:00-03:00"), DateTimeOffset.Parse("2026-09-10T10:00:00-03:00"), "America/Sao_Paulo", "desc", "Sala", ["a@example.com"]);

        CalendarEvent read = await client.GetEventAsync(connectionId, "one");
        await client.CreateEventAsync(mutation);
        await client.UpdateEventAsync("three", mutation);
        await client.DeleteEventAsync(connectionId, "four");

        Assert.Equal("details", read.Description);
        Assert.NotNull(read.Start.DateTime);
        Assert.Equal([HttpMethod.Get, HttpMethod.Post, HttpMethod.Patch, HttpMethod.Delete], handler.Methods);
        Assert.Equal("/calendar/v3/calendars/primary/events/one", handler.Uris[0]!.AbsolutePath);
        Assert.Contains("sendUpdates=none", handler.Uris[1]!.Query);
        Assert.Contains("sendUpdates=none", handler.Uris[2]!.Query);
        Assert.Contains("sendUpdates=none", handler.Uris[3]!.Query);
        Assert.Contains("\"summary\":\"Evento\"", handler.Bodies[1]);
        Assert.Contains("\"attendees\":[{\"email\":\"a@example.com\"}]", handler.Bodies[1]);
    }

    [Fact]
    public async Task Failures_DoNotExposeProviderBodyOrToken()
    {
        const string providerSecret = "PROVIDER-SECRET";
        var handler = new RecordingHandler(providerSecret, HttpStatusCode.Forbidden);
        var client = CreateClient(handler);

        CalendarApiException error = await Assert.ThrowsAsync<CalendarApiException>(
            () => client.GetEventAsync(Guid.NewGuid(), "event"));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.DoesNotContain(providerSecret, error.Message);
        Assert.DoesNotContain("calendar-test-token", error.Message);
    }

    [Fact]
    public async Task MissingToken_DoesNotCallCalendar()
    {
        var handler = new RecordingHandler("{}");
        var client = new CalendarClient(new Factory(handler), new Tokens(OperationResult<GoogleAccessToken>.Failure("not authenticated")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetEventAsync(Guid.NewGuid(), "event"));

        Assert.Empty(handler.Methods);
    }

    private static CalendarClient CreateClient(RecordingHandler handler) =>
        new(new Factory(handler), new Tokens(OperationResult<GoogleAccessToken>.Success(new("calendar-test-token"))));

    private sealed class Tokens(OperationResult<GoogleAccessToken> result) : IGoogleOAuthTokenService
    {
        public Task<OperationResult<GoogleAccessToken>> GetAccessTokenAsync(Guid connectionId, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class Factory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(CalendarClient.HttpClientName, name);
            return new HttpClient(handler, false);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<(string Body, HttpStatusCode Status)> _responses;
        public RecordingHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : this([(response, status)]) { }
        public RecordingHandler(params string[] responses) : this(responses.Select(response => (response, HttpStatusCode.OK))) { }
        private RecordingHandler(IEnumerable<(string Body, HttpStatusCode Status)> responses) => _responses = new(responses);
        public List<HttpMethod> Methods { get; } = [];
        public List<Uri?> Uris { get; } = [];
        public List<string> Bodies { get; } = [];
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public Uri? Uri => Uris.FirstOrDefault();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method); Uris.Add(request.RequestUri);
            Authorization = request.Headers.Authorization;
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            (string body, HttpStatusCode status) = _responses.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
