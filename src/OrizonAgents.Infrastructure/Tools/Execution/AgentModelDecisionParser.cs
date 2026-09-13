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

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty(
                    "action",
                    out JsonElement actionElement) ||
                actionElement.ValueKind != JsonValueKind.String)
            {
                return AgentModelDecision.FinalResponse(normalized);
            }

            string? action = actionElement.GetString();

            if (string.Equals(
                action,
                "tool_call",
                StringComparison.OrdinalIgnoreCase))
            {
                return TryParseToolCall(
                    root,
                    out AgentToolCall toolCall)
                    ? AgentModelDecision.RequestTool(toolCall)
                    : AgentModelDecision.FinalResponse(normalized);
            }

            if (string.Equals(
                action,
                "tool_calls",
                StringComparison.OrdinalIgnoreCase))
            {
                return TryParseToolCalls(
                    root,
                    out IReadOnlyList<AgentToolCall> toolCalls)
                    ? AgentModelDecision.RequestTools(toolCalls)
                    : AgentModelDecision.FinalResponse(normalized);
            }

            return AgentModelDecision.FinalResponse(normalized);
        }
        catch (JsonException)
        {
            return AgentModelDecision.FinalResponse(normalized);
        }
    }

    private static bool TryParseToolCalls(
        JsonElement root,
        out IReadOnlyList<AgentToolCall> toolCalls)
    {
        toolCalls = [];

        if (!root.TryGetProperty(
                "calls",
                out JsonElement callsElement) ||
            callsElement.ValueKind != JsonValueKind.Array ||
            callsElement.GetArrayLength() == 0)
        {
            return false;
        }

        var parsedCalls = new List<AgentToolCall>();

        foreach (JsonElement callElement in callsElement.EnumerateArray())
        {
            if (!TryParseToolCall(
                    callElement,
                    out AgentToolCall toolCall))
            {
                return false;
            }

            parsedCalls.Add(toolCall);
        }

        toolCalls = parsedCalls;
        return true;
    }

    private static bool TryParseToolCall(
        JsonElement element,
        out AgentToolCall toolCall)
    {
        toolCall = null!;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(
                "toolId",
                out JsonElement toolIdElement) ||
            toolIdElement.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(
                toolIdElement.GetString(),
                out Guid toolId))
        {
            return false;
        }

        JsonElement? input = null;

        if (element.TryGetProperty(
                "input",
                out JsonElement inputElement))
        {
            input = inputElement.Clone();
        }

        toolCall = new AgentToolCall(toolId, input);
        return true;
    }
}
