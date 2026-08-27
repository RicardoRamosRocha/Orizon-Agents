using System.Text.Json;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class AgentModelDecisionParser : IAgentModelDecisionParser
{
    public AgentModelDecision Parse(string modelResponse)
    {
        if (string.IsNullOrWhiteSpace(modelResponse))
        {
            return AgentModelDecision.FinalResponse(string.Empty);
        }

        string normalized = modelResponse.Trim();

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(normalized);

            JsonElement root = document.RootElement;

            if (!root.TryGetProperty(
                    "action",
                    out JsonElement actionElement))
            {
                return AgentModelDecision.FinalResponse(normalized);
            }

            string? action = actionElement.GetString();

            if (!string.Equals(
                    action,
                    "tool_call",
                    StringComparison.OrdinalIgnoreCase))
            {
                return AgentModelDecision.FinalResponse(normalized);
            }

            if (!root.TryGetProperty(
                    "toolId",
                    out JsonElement toolIdElement) ||
                !Guid.TryParse(
                    toolIdElement.GetString(),
                    out Guid toolId))
            {
                return AgentModelDecision.FinalResponse(normalized);
            }

            JsonElement? input = null;

            if (root.TryGetProperty(
                    "input",
                    out JsonElement inputElement))
            {
                input = inputElement.Clone();
            }

            return AgentModelDecision.RequestTool(
                new AgentToolCall(
                    toolId,
                    input));
        }
        catch (JsonException)
        {
            return AgentModelDecision.FinalResponse(normalized);
        }
    }
}
