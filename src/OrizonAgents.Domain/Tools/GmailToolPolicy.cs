namespace OrizonAgents.Domain.Tools;

public static class GmailToolPolicy
{
    public static bool IsGmail(AgentToolKind kind) => kind is
        AgentToolKind.GmailSearch or
        AgentToolKind.GmailReadMessage or
        AgentToolKind.GmailCreateDraft or
        AgentToolKind.GmailSend or
        AgentToolKind.GmailReply;

    public static AgentToolRiskLevel RequiredRiskLevel(AgentToolKind kind) => kind switch
    {
        AgentToolKind.GmailSearch or AgentToolKind.GmailReadMessage =>
            AgentToolRiskLevel.Read,
        AgentToolKind.GmailCreateDraft => AgentToolRiskLevel.Write,
        AgentToolKind.GmailSend or AgentToolKind.GmailReply =>
            AgentToolRiskLevel.Sensitive,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
