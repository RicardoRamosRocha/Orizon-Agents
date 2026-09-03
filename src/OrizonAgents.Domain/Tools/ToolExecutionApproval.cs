using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Tools;

public sealed class ToolExecutionApproval : AuditableEntity, ITenantOwnedEntity
{
    public const int InputHashMaxLength = 128;

    private ToolExecutionApproval()
    {
        InputHash = string.Empty;
    }

    public ToolExecutionApproval(
        Guid tenantId,
        Guid agentId,
        Guid toolId,
        string inputHash,
        DateTime expiresAtUtc)
    {
        EnsureIdentifier(tenantId, nameof(tenantId));
        EnsureIdentifier(agentId, nameof(agentId));
        EnsureIdentifier(toolId, nameof(toolId));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));

        if (string.IsNullOrWhiteSpace(inputHash))
        {
            throw new ArgumentException(
                "InputHash é obrigatório.",
                nameof(inputHash));
        }

        string normalizedHash = inputHash.Trim();

        if (normalizedHash.Length > InputHashMaxLength)
        {
            throw new ArgumentException(
                $"InputHash excede {InputHashMaxLength} caracteres.",
                nameof(inputHash));
        }

        TenantId = tenantId;
        AgentId = agentId;
        ToolId = toolId;
        InputHash = normalizedHash;
        Status = ToolExecutionApprovalStatus.Pending;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid TenantId { get; private set; }
    public Guid AgentId { get; private set; }
    public Guid ToolId { get; private set; }
    public string InputHash { get; private set; }
    public ToolExecutionApprovalStatus Status { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

    public void Approve(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsurePending();

        if (utcNow >= ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Não é possível aprovar uma solicitação expirada.");
        }

        Status = ToolExecutionApprovalStatus.Approved;
        ApprovedAtUtc = utcNow;
    }

    public void Reject(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsurePending();

        Status = ToolExecutionApprovalStatus.Rejected;
        RejectedAtUtc = utcNow;
    }

    public void Consume(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));

        if (Status != ToolExecutionApprovalStatus.Approved)
        {
            throw new InvalidOperationException(
                "Somente uma aprovação aprovada pode ser consumida.");
        }

        if (utcNow >= ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "Não é possível consumir uma aprovação expirada.");
        }

        Status = ToolExecutionApprovalStatus.Consumed;
        ConsumedAtUtc = utcNow;
    }

    public void Expire(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));
        EnsurePending();

        Status = ToolExecutionApprovalStatus.Expired;
    }

    private void EnsurePending()
    {
        if (Status != ToolExecutionApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                "A solicitação não está pendente.");
        }
    }

    private static void EnsureIdentifier(
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador é obrigatório.",
                parameterName);
        }
    }

    private static void EnsureUtc(
        DateTime value,
        string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "A data deve estar em UTC.",
                parameterName);
        }
    }
}
