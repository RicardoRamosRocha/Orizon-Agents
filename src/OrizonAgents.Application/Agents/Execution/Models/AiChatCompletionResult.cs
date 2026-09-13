using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiChatCompletionResult(
    string Content,
    AiChatUsage? Usage = null)
{
    public IReadOnlyList<AgentToolCall> ToolCalls { get; init; } = [];

    public string? ContinuationToken { get; init; }
}

public sealed record AiChatUsage(
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens);
