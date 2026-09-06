namespace OrizonAgents.Application.Agents.Execution.Context;

public interface IAgentContextBudget
{
    string ReduceToolResult(
        string content,
        int remainingCharacters);
}
