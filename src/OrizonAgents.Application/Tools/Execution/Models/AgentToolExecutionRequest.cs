using System.Text.Json;

namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record AgentToolExecutionRequest(
    Guid AgentId,
    Guid ToolId,
    JsonElement? Input = null);
