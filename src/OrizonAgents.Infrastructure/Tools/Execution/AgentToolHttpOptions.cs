namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class AgentToolHttpOptions
{
    public const string SectionName = "AgentTools:Http";

    public bool AllowLocalhost { get; set; }

    public bool AllowPrivateNetworks { get; set; }
}
