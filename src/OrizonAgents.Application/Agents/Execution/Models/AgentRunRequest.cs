using System.Text.Json;

namespace OrizonAgents.Application.Agents.Execution.Models;

public sealed record AgentRunRequest(
    string Message,
    Guid? ConversationId = null,
    JsonElement? Context = null);
