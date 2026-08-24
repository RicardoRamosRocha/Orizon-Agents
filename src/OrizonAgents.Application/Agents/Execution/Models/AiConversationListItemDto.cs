namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiConversationListItemDto(
    Guid Id,
    Guid AgentId,
    string? Title,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
