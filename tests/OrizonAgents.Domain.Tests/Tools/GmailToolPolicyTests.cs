using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Domain.Tests.Tools;

public sealed class GmailToolPolicyTests
{
    [Theory]
    [InlineData(AgentToolKind.GmailSearch, AgentToolRiskLevel.Read)]
    [InlineData(AgentToolKind.GmailReadMessage, AgentToolRiskLevel.Read)]
    [InlineData(AgentToolKind.GmailCreateDraft, AgentToolRiskLevel.Write)]
    [InlineData(AgentToolKind.GmailSend, AgentToolRiskLevel.Sensitive)]
    [InlineData(AgentToolKind.GmailReply, AgentToolRiskLevel.Sensitive)]
    public void RequiredRiskLevel_IsFixedByGmailOperation(
        AgentToolKind kind,
        AgentToolRiskLevel riskLevel)
    {
        Assert.True(GmailToolPolicy.IsGmail(kind));
        Assert.Equal(riskLevel, GmailToolPolicy.RequiredRiskLevel(kind));
    }
}
