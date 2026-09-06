using OrizonAgents.Application.Agents.Execution.Context;

namespace OrizonAgents.Infrastructure.Agents.Execution.Context;

public sealed class AgentContextBudget : IAgentContextBudget
{
    public const int MaximumCharactersPerToolResult = 12_000;

    public string ReduceToolResult(
        string content,
        int remainingCharacters)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            remainingCharacters <= 0)
        {
            return string.Empty;
        }

        string normalized = content.Trim();

        int allowedCharacters = Math.Min(
            MaximumCharactersPerToolResult,
            remainingCharacters);

        if (normalized.Length <= allowedCharacters)
        {
            return normalized;
        }

        const string truncationMarker =
            "\n\n[Conteúdo reduzido pelo limite de contexto.]";

        if (allowedCharacters <= truncationMarker.Length)
        {
            return truncationMarker[..allowedCharacters];
        }

        int contentLength =
            allowedCharacters - truncationMarker.Length;

        return normalized[..contentLength] +
            truncationMarker;
    }
}
