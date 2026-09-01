namespace OrizonAgents.API.Contracts.Agents;

public sealed record RunAgentRequest(string? Message)
{
    public const int MessageMaxLength = 12000;
}
