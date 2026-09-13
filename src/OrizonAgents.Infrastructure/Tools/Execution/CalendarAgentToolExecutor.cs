using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Integrations.Calendar;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Integrations.Calendar;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class CalendarAgentToolExecutor(ICalendarClient calendar, IGoogleOAuthCapabilityService capabilities, ILogger<CalendarAgentToolExecutor> logger)
{
    public async Task<AgentToolExecutionResult> ExecuteAsync(AgentTool tool, JsonElement? input, CancellationToken ct = default)
    {
        if (!CalendarToolPolicy.IsCalendar(tool.Kind) || tool.IntegrationConnectionId is not Guid connectionId || !input.HasValue || input.Value.ValueKind != JsonValueKind.Object)
            return AgentToolExecutionResult.Failure("Os argumentos da Tool Calendar são inválidos.");
        if (!await capabilities.HasCapabilityAsync(connectionId, RequiredCapability(tool.Kind), ct))
            return AgentToolExecutionResult.Failure("A conexão Google não possui autorização necessária para o Calendar.");
        try
        {
            JsonElement value = input.Value;
            object result = tool.Kind switch
            {
                AgentToolKind.CalendarSearch => await calendar.SearchEventsAsync(new(connectionId, Optional(value,"query"), OptionalDate(value,"timeMin"), OptionalDate(value,"timeMax"), OptionalInt(value,"maxResults") ?? 10), ct),
                AgentToolKind.CalendarReadEvent => await calendar.GetEventAsync(connectionId, Required(value,"eventId"), ct),
                AgentToolKind.CalendarCreateEvent => await calendar.CreateEventAsync(Mutation(connectionId,value), ct),
                AgentToolKind.CalendarUpdateEvent => await calendar.UpdateEventAsync(Required(value,"eventId"), Mutation(connectionId,value), ct),
                AgentToolKind.CalendarDeleteEvent => await DeleteAsync(connectionId, Required(value,"eventId"), ct),
                _ => throw new ArgumentException()
            };
            return AgentToolExecutionResult.Success(null, JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        catch (Exception ex) when (ex is ArgumentException or CalendarApiException or InvalidOperationException or JsonException)
        { logger.LogWarning("Calendar Tool {ToolId} failed: {Type}", tool.Id, ex.GetType().Name); return AgentToolExecutionResult.Failure("Não foi possível executar a Tool Calendar."); }
    }
    private async Task<object> DeleteAsync(Guid connectionId,string eventId,CancellationToken ct) { await calendar.DeleteEventAsync(connectionId,eventId,ct); return new { eventId, deleted = true }; }
    private static CalendarEventMutation Mutation(Guid connectionId, JsonElement x) => new(connectionId, Required(x,"summary"), RequiredDate(x,"start"), RequiredDate(x,"end"), Optional(x,"timeZone"), Optional(x,"description"), Optional(x,"location"), Attendees(x));
    private static IReadOnlyList<string>? Attendees(JsonElement x) => x.TryGetProperty("attendees",out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray().Select(v=>v.ValueKind==JsonValueKind.String?v.GetString():null).Where(v=>!string.IsNullOrWhiteSpace(v)).Cast<string>().Take(CalendarToolPolicy.MaximumAttendees).ToArray() : null;
    private static string Required(JsonElement x,string n) => Optional(x,n) is { Length: > 0 } v ? v : throw new ArgumentException();
    private static string? Optional(JsonElement x,string n) => x.TryGetProperty(n,out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;
    private static int? OptionalInt(JsonElement x,string n) => x.TryGetProperty(n,out var v) && v.TryGetInt32(out int value) && value is >= 1 and <= CalendarToolPolicy.MaximumSearchResults ? value : x.TryGetProperty(n,out _) ? throw new ArgumentException() : null;
    private static DateTimeOffset RequiredDate(JsonElement x,string n) => OptionalDate(x,n) ?? throw new ArgumentException();
    private static DateTimeOffset? OptionalDate(JsonElement x,string n) => DateTimeOffset.TryParse(Optional(x,n),out var value) ? value : Optional(x,n) is null ? null : throw new ArgumentException();
    private static GoogleOAuthCapability RequiredCapability(AgentToolKind kind) => kind is AgentToolKind.CalendarSearch or AgentToolKind.CalendarReadEvent ? GoogleOAuthCapability.CalendarRead : GoogleOAuthCapability.CalendarWrite;
}
