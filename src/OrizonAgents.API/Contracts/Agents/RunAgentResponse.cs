using System.Text.Json.Serialization;

namespace OrizonAgents.API.Contracts.Agents;

public sealed record RunAgentResponse(
    bool Success,
    string Response,
    Guid ConversationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Status = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? ApprovalId = null);
