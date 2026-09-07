namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AiChatCompletionResult(
    string Content,
    AiChatUsage? Usage = null);

public sealed record AiChatUsage(
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens);
