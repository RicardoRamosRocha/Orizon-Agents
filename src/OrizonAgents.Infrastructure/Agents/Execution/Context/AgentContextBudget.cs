using OrizonAgents.Application.Agents.Execution.Context;

namespace OrizonAgents.Infrastructure.Agents.Execution.Context;

public sealed class AgentContextBudget : IAgentContextBudget
{
    public const int MaximumCharactersPerExecution = 32_000;
    public const int MaximumCharactersPerToolResult = 12_000;

    public IAgentExecutionContextBudget Begin(string? operationalContext)
    {
        string? reducedContext = string.IsNullOrWhiteSpace(operationalContext)
            ? null
            : ReduceContent(operationalContext, MaximumCharactersPerExecution);
        return new ExecutionContextBudget(this, reducedContext);
    }

    public string ReduceToolResult(
        string content,
        int remainingCharacters)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            remainingCharacters <= 0)
        {
            return string.Empty;
        }

        int allowedCharacters = Math.Min(
            MaximumCharactersPerToolResult,
            remainingCharacters);

        return ReduceContent(content, allowedCharacters);
    }

    private static string ReduceContent(string content, int allowedCharacters)
    {
        string normalized = content.Trim();

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

    private sealed class ExecutionContextBudget(
        AgentContextBudget budget,
        string? operationalContext) : IAgentExecutionContextBudget
    {
        private int _usedCharacters = operationalContext?.Length ?? 0;

        public string? OperationalContext { get; } = operationalContext;

        public int RemainingCharacters =>
            Math.Max(0, MaximumCharactersPerExecution - _usedCharacters);

        public bool IsExhausted => RemainingCharacters == 0;

        public string ReduceAndConsumeToolResult(string content)
        {
            string reduced = budget.ReduceToolResult(content, RemainingCharacters);
            _usedCharacters += reduced.Length;
            return reduced;
        }
    }
}
