namespace OrizonAgents.API.Contracts.Agents;

public sealed record RunAgentRequest(
    string? Message,
    Guid? ConversationId = null)
{
    public const int MessageMaxLength = 12000;
}
