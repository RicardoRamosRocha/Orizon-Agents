namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiAgentRunResult(
    Guid ConversationId,
    string Response);
