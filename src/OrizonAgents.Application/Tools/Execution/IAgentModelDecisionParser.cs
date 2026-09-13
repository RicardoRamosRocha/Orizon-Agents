using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Application.Tools.Execution;

public interface IAgentModelDecisionParser
{
    AgentModelDecision Parse(string modelResponse);
}
