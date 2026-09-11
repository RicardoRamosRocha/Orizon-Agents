namespace OrizonAgents.Domain.Tools;

public static class CalendarToolPolicy
{
    public const int MaximumSearchResults = 20;
    public const int MaximumAttendees = 20;

    public static bool IsCalendar(AgentToolKind kind) => kind is
        AgentToolKind.CalendarSearch or AgentToolKind.CalendarReadEvent or
        AgentToolKind.CalendarCreateEvent or AgentToolKind.CalendarUpdateEvent or
        AgentToolKind.CalendarDeleteEvent;

    public static AgentToolRiskLevel RequiredRiskLevel(AgentToolKind kind) => kind switch
    {
        AgentToolKind.CalendarSearch or AgentToolKind.CalendarReadEvent => AgentToolRiskLevel.Read,
        AgentToolKind.CalendarCreateEvent or AgentToolKind.CalendarUpdateEvent or AgentToolKind.CalendarDeleteEvent => AgentToolRiskLevel.Sensitive,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

}
