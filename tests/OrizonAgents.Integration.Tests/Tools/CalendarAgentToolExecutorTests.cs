using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Integrations.Calendar;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class CalendarAgentToolExecutorTests
{
    [Fact]
    public async Task Search_UsesConfiguredConnectionAndCalendarReadCapability()
    {
        Guid configuredConnection = Guid.NewGuid();
        var calendar = new StubCalendarClient();
        var capabilities = new Capabilities(true);
        var executor = new CalendarAgentToolExecutor(calendar, capabilities, NullLogger<CalendarAgentToolExecutor>.Instance);

        var result = await executor.ExecuteAsync(Tool(AgentToolKind.CalendarSearch, configuredConnection), Json("{" + "\"query\":\"contract\",\"connectionId\":\"" + Guid.NewGuid() + "\"}"));

        Assert.True(result.Succeeded);
        Assert.Equal(configuredConnection, calendar.ConnectionId);
        Assert.Equal(GoogleOAuthCapability.CalendarRead, capabilities.Capability);
        Assert.DoesNotContain(configuredConnection.ToString(), result.Content!);
        Assert.DoesNotContain("calendar-test-token", result.Content!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingCapability_StopsBeforeCalendarCall()
    {
        var calendar = new StubCalendarClient();
        var executor = new CalendarAgentToolExecutor(calendar, new Capabilities(false), NullLogger<CalendarAgentToolExecutor>.Instance);

        var result = await executor.ExecuteAsync(Tool(AgentToolKind.CalendarReadEvent, Guid.NewGuid()), Json("{\"eventId\":\"event\"}"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, calendar.Calls);
    }

    [Fact]
    public async Task Mutation_RequiresCalendarWriteAndRejectsInvalidInputBeforeCall()
    {
        var calendar = new StubCalendarClient();
        var capabilities = new Capabilities(true);
        var executor = new CalendarAgentToolExecutor(calendar, capabilities, NullLogger<CalendarAgentToolExecutor>.Instance);

        var result = await executor.ExecuteAsync(Tool(AgentToolKind.CalendarCreateEvent, Guid.NewGuid()), Json("{\"summary\":\"x\",\"start\":\"invalid\",\"end\":\"2026-09-10T10:00:00-03:00\"}"));

        Assert.False(result.Succeeded);
        Assert.Equal(GoogleOAuthCapability.CalendarWrite, capabilities.Capability);
        Assert.Equal(0, calendar.Calls);
    }

    private static AgentTool Tool(AgentToolKind kind, Guid connection)
    {
        var tool = new AgentTool(Guid.NewGuid(), "Calendar", "Calendar tool", "https://internal/calendar");
        tool.ConfigureKind(kind, connection);
        tool.SetRiskLevel(CalendarToolPolicy.RequiredRiskLevel(kind));
        return tool;
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private sealed class Capabilities(bool granted) : IGoogleOAuthCapabilityService
    {
        public GoogleOAuthCapability? Capability { get; private set; }
        public Task<bool> HasCapabilityAsync(Guid connectionId, GoogleOAuthCapability capability, CancellationToken cancellationToken = default)
        { Capability = capability; return Task.FromResult(granted); }
    }

    private sealed class StubCalendarClient : ICalendarClient
    {
        public int Calls { get; private set; }
        public Guid? ConnectionId { get; private set; }
        public Task<CalendarSearchResult> SearchEventsAsync(CalendarSearchRequest request, CancellationToken cancellationToken = default)
        { Calls++; ConnectionId = request.ConnectionId; return Task.FromResult(new CalendarSearchResult([], null)); }
        public Task<CalendarEvent> GetEventAsync(Guid connectionId, string eventId, CancellationToken cancellationToken = default) => Event(connectionId);
        public Task<CalendarEvent> CreateEventAsync(CalendarEventMutation request, CancellationToken cancellationToken = default) => Event(request.ConnectionId);
        public Task<CalendarEvent> UpdateEventAsync(string eventId, CalendarEventMutation request, CancellationToken cancellationToken = default) => Event(request.ConnectionId);
        public Task DeleteEventAsync(Guid connectionId, string eventId, CancellationToken cancellationToken = default) { Calls++; ConnectionId = connectionId; return Task.CompletedTask; }
        private Task<CalendarEvent> Event(Guid connectionId) { Calls++; ConnectionId = connectionId; return Task.FromResult(new CalendarEvent("event", "summary", new(null, new DateOnly(2026, 9, 10)), new(null, new DateOnly(2026, 9, 11)), null, null, [], null, null, "confirmed", null)); }
    }
}
