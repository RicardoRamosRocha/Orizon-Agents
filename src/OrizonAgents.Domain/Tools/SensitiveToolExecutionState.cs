namespace OrizonAgents.Domain.Tools;

public enum SensitiveToolExecutionState
{
    AwaitingApproval = 1,
    Ready = 2,
    Executing = 3,
    Executed = 4,
    Failed = 5,
    OutcomeUnknown = 6,
    Rejected = 7,
    Expired = 8
}
