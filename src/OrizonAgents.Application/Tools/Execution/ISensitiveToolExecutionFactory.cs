using System.Text.Json;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Application.Tools.Execution;

public interface ISensitiveToolExecutionFactory
{
    Task<SensitiveToolExecution> CreateAsync(
        ToolExecutionApproval approval,
        AgentTool tool,
        AgentToolBinding binding,
        JsonElement? validatedInput,
        CancellationToken cancellationToken = default);
}
