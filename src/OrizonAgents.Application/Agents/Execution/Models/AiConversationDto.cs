namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiConversationDto(
    Guid Id,
    Guid AgentId,
    string? Title,
    IReadOnlyList<AiConversationMessageDto> Messages);

public sealed record AiConversationMessageDto(
    string Role,
    string Content,
    DateTime CreatedAtUtc);
