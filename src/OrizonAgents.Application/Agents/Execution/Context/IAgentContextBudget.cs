namespace OrizonAgents.Application.Agents.Execution.Context;

public interface IAgentContextBudget
{
    IAgentExecutionContextBudget Begin(string? operationalContext);

    string ReduceToolResult(
        string content,
        int remainingCharacters);
}

public interface IAgentExecutionContextBudget
{
    string? OperationalContext { get; }

    int RemainingCharacters { get; }

    bool IsExhausted { get; }

    string ReduceAndConsumeToolResult(string content);
}
