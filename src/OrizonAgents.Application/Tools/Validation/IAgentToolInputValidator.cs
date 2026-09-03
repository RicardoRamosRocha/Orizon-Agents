using System.Text.Json;

namespace OrizonAgents.Application.Tools.Validation;

public interface IAgentToolInputValidator
{
    AgentToolInputValidationResult Validate(
        string? inputSchema,
        JsonElement? input);
}
