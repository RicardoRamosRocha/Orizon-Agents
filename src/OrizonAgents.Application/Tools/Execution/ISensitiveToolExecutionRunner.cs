using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Application.Tools.Execution;

public interface ISensitiveToolExecutionRunner
{
    Task<SensitiveToolExecutionRunResult> RunAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}
