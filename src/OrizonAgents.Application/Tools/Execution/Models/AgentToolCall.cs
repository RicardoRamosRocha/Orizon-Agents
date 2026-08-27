using System.Text.Json;

namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record AgentToolCall(
    Guid ToolId,
    JsonElement? Input);
