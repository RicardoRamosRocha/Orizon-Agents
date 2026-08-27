namespace OrizonAgents.Application.Tools.Execution.Models;

public enum AgentModelDecisionType
{
    Response = 1,
    ToolCall = 2
}

public sealed record AgentModelDecision(
    AgentModelDecisionType Type,
    string? Response,
    AgentToolCall? ToolCall)
{
    public static AgentModelDecision FinalResponse(string response)
    {
        return new AgentModelDecision(
            AgentModelDecisionType.Response,
            response,
            null);
    }

    public static AgentModelDecision RequestTool(
        AgentToolCall toolCall)
    {
        return new AgentModelDecision(
            AgentModelDecisionType.ToolCall,
            null,
            toolCall);
    }
}
