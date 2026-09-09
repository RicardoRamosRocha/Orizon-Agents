using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tools;

public sealed class SensitiveToolExecution : AuditableEntity, ITenantOwnedEntity
{
    public const int ProtectedArgumentsMaxLength = 32768;
    public const int InputFingerprintMaxLength = 128;
    public const int ToolConfigurationFingerprintMaxLength = 128;
    public const int ConcurrencyStampMaxLength = 32;

    private SensitiveToolExecution()
    {
        ProtectedArguments = string.Empty;
        InputFingerprint = string.Empty;
        ToolConfigurationFingerprint = string.Empty;
    }

    public SensitiveToolExecution(
        Guid executionId,
        Guid tenantId,
        ToolExecutionApproval approval,
        Guid agentId,
        Guid toolId,
        Guid agentToolBindingId,
        AgentToolKind toolKind,
        Guid? integrationConnectionId,
        string protectedArguments,
        string inputFingerprint,
        string toolConfigurationFingerprint,
        DateTime expiresAtUtc)
    {
        EnsureIdentifier(executionId, nameof(executionId));
        EnsureIdentifier(tenantId, nameof(tenantId));
        EnsureIdentifier(agentId, nameof(agentId));
        EnsureIdentifier(toolId, nameof(toolId));
        EnsureIdentifier(agentToolBindingId, nameof(agentToolBindingId));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));

        if (!Enum.IsDefined(toolKind))
        {
            throw new ArgumentOutOfRangeException(nameof(toolKind));
        }

        if (approval is null)
        {
            throw new ArgumentNullException(nameof(approval));
        }

        if (approval.TenantId != tenantId)
        {
            throw new ArgumentException(
                "The approval must belong to the execution tenant.",
                nameof(approval));
        }

        if (integrationConnectionId == Guid.Empty)
        {
            throw new ArgumentException("Integration connection identifier is invalid.", nameof(integrationConnectionId));
        }

        Id = executionId;
        TenantId = tenantId;
        Approval = approval;
        ApprovalId = approval.Id;
        AgentId = agentId;
        ToolId = toolId;
        AgentToolBindingId = agentToolBindingId;
        ToolKind = toolKind;
        IntegrationConnectionId = integrationConnectionId;
        ProtectedArguments = NormalizeRequired(
            protectedArguments,
            ProtectedArgumentsMaxLength,
            nameof(protectedArguments));
        InputFingerprint = NormalizeRequired(
            inputFingerprint,
            InputFingerprintMaxLength,
            nameof(inputFingerprint));
        ToolConfigurationFingerprint = NormalizeRequired(
            toolConfigurationFingerprint,
            ToolConfigurationFingerprintMaxLength,
            nameof(toolConfigurationFingerprint));
        ExpiresAtUtc = expiresAtUtc;
        State = SensitiveToolExecutionState.AwaitingApproval;
    }

    public Guid TenantId { get; private set; }
    public Guid ApprovalId { get; private set; }
    public ToolExecutionApproval? Approval { get; private set; }
    public Guid AgentId { get; private set; }
    public Guid ToolId { get; private set; }
    public Guid AgentToolBindingId { get; private set; }
    public AgentToolKind ToolKind { get; private set; }
    public Guid? IntegrationConnectionId { get; private set; }
    public string ProtectedArguments { get; private set; }
    public string InputFingerprint { get; private set; }
    public string ToolConfigurationFingerprint { get; private set; }
    public SensitiveToolExecutionState State { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ExecutionStartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");

    public void MarkReady(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureState(SensitiveToolExecutionState.AwaitingApproval);
        EnsureNotExpired(utcNow);
        State = SensitiveToolExecutionState.Ready;
        RenewConcurrencyStamp();
    }

    public void BeginExecution(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureState(SensitiveToolExecutionState.Ready);
        EnsureNotExpired(utcNow);
        State = SensitiveToolExecutionState.Executing;
        ExecutionStartedAtUtc = utcNow;
        RenewConcurrencyStamp();
    }

    public void Complete(DateTime utcNow)
    {
        TransitionFromExecuting(SensitiveToolExecutionState.Executed, utcNow);
    }

    public void Fail(DateTime utcNow)
    {
        TransitionFromExecuting(SensitiveToolExecutionState.Failed, utcNow);
    }

    public void MarkOutcomeUnknown(DateTime utcNow)
    {
        TransitionFromExecuting(SensitiveToolExecutionState.OutcomeUnknown, utcNow);
    }

    public void Reject(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureState(SensitiveToolExecutionState.AwaitingApproval);
        State = SensitiveToolExecutionState.Rejected;
        CompletedAtUtc = utcNow;
        RenewConcurrencyStamp();
    }

    public void Expire(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        if (State is not SensitiveToolExecutionState.AwaitingApproval and
            not SensitiveToolExecutionState.Ready)
        {
            throw new InvalidOperationException("The sensitive Tool execution cannot expire in its current state.");
        }

        State = SensitiveToolExecutionState.Expired;
        CompletedAtUtc = utcNow;
        RenewConcurrencyStamp();
    }

    private void TransitionFromExecuting(
        SensitiveToolExecutionState targetState,
        DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsureState(SensitiveToolExecutionState.Executing);
        State = targetState;
        CompletedAtUtc = utcNow;
        RenewConcurrencyStamp();
    }

    private void EnsureNotExpired(DateTime utcNow)
    {
        if (utcNow >= ExpiresAtUtc)
        {
            throw new InvalidOperationException("The sensitive Tool execution has expired.");
        }
    }

    private void EnsureState(SensitiveToolExecutionState expectedState)
    {
        if (State != expectedState)
        {
            throw new InvalidOperationException("The sensitive Tool execution is not in the expected state.");
        }
    }

    private static void EnsureIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An identifier is required.", parameterName);
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The date must be UTC.", parameterName);
        }
    }

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException("The value exceeds the permitted limit.", parameterName);
    }

    private void RenewConcurrencyStamp() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}
