namespace OrizonAgents.Application.Tools.Execution.Models;

public sealed record SensitiveToolExecutionRunResult(
    SensitiveToolExecutionRunStatus Status)
{
    public static SensitiveToolExecutionRunResult Executed() =>
        new(SensitiveToolExecutionRunStatus.Executed);

    public static SensitiveToolExecutionRunResult Failed() =>
        new(SensitiveToolExecutionRunStatus.Failed);

    public static SensitiveToolExecutionRunResult OutcomeUnknown() =>
        new(SensitiveToolExecutionRunStatus.OutcomeUnknown);

    public static SensitiveToolExecutionRunResult NotAvailable() =>
        new(SensitiveToolExecutionRunStatus.NotAvailable);
}
