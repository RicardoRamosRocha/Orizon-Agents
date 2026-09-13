using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class CalendarToolPolicyTests
{
    [Theory]
    [InlineData(AgentToolKind.CalendarSearch, AgentToolRiskLevel.Read)]
    [InlineData(AgentToolKind.CalendarReadEvent, AgentToolRiskLevel.Read)]
    [InlineData(AgentToolKind.CalendarCreateEvent, AgentToolRiskLevel.Sensitive)]
    [InlineData(AgentToolKind.CalendarUpdateEvent, AgentToolRiskLevel.Sensitive)]
    [InlineData(AgentToolKind.CalendarDeleteEvent, AgentToolRiskLevel.Sensitive)]
    public void RequiredRiskLevel_IsCentralized(AgentToolKind kind, AgentToolRiskLevel risk) =>
        Assert.Equal(risk, CalendarToolPolicy.RequiredRiskLevel(kind));

    [Theory]
    [InlineData(AgentToolKind.CalendarSearch, GoogleOAuthCapability.CalendarRead)]
    [InlineData(AgentToolKind.CalendarReadEvent, GoogleOAuthCapability.CalendarRead)]
    [InlineData(AgentToolKind.CalendarCreateEvent, GoogleOAuthCapability.CalendarWrite)]
    [InlineData(AgentToolKind.CalendarUpdateEvent, GoogleOAuthCapability.CalendarWrite)]
    [InlineData(AgentToolKind.CalendarDeleteEvent, GoogleOAuthCapability.CalendarWrite)]
    public void CalendarKinds_MapToExpectedCapabilities(AgentToolKind kind, GoogleOAuthCapability capability)
    {
        Assert.True(CalendarToolPolicy.IsCalendar(kind));
        Assert.Equal(capability, kind is AgentToolKind.CalendarSearch or AgentToolKind.CalendarReadEvent
            ? GoogleOAuthCapability.CalendarRead : GoogleOAuthCapability.CalendarWrite);
    }
}
