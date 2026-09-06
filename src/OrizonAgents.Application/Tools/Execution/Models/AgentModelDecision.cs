namespace OrizonAgents.Application.Tools.Execution.Models;

public enum AgentModelDecisionType
{
    Response = 1,
    ToolCall = 2
}

public sealed record AgentModelDecision(
    AgentModelDecisionType Type,
    string? Response,
    IReadOnlyList<AgentToolCall> ToolCalls)
{
    public AgentToolCall? ToolCall =>
        ToolCalls.Count == 1
            ? ToolCalls[0]
            : null;

    public static AgentModelDecision FinalResponse(string response)
    {
        return new AgentModelDecision(
            AgentModelDecisionType.Response,
            response,
            []);
    }

    public static AgentModelDecision RequestTool(
        AgentToolCall toolCall)
    {
        return RequestTools([toolCall]);
    }

    public static AgentModelDecision RequestTools(
        IReadOnlyList<AgentToolCall> toolCalls)
    {
        ArgumentNullException.ThrowIfNull(toolCalls);

        if (toolCalls.Count == 0)
        {
            throw new ArgumentException(
                "Ao menos uma ToolCall é obrigatória.",
                nameof(toolCalls));
        }

        return new AgentModelDecision(
            AgentModelDecisionType.ToolCall,
            null,
            toolCalls);
    }
}
