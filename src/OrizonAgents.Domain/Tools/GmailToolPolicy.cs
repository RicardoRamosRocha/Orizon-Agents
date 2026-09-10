namespace OrizonAgents.Domain.Tools;

public static class GmailToolPolicy
{
    // GmailSearch is a selection Tool. Keep its compact result bounded before
    // calling Gmail so the Agent never receives an unbounded result list.
    public const int MaximumSearchResultsForAgent = 20;

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
