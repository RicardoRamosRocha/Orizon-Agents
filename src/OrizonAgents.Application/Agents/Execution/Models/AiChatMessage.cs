namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiChatMessage(
    string Role,
    string Content);
