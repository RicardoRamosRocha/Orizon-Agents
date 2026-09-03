using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Domain.Tests.Tools;

public sealed class ToolExecutionApprovalTests
{
    [Fact]
    public void Constructor_ShouldCreatePendingApproval()
    {
        Guid tenantId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        Guid toolId = Guid.NewGuid();
        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(10);

        var approval = new ToolExecutionApproval(
            tenantId,
            agentId,
            toolId,
            "input-hash",
            expiresAtUtc);

        Assert.Equal(tenantId, approval.TenantId);
        Assert.Equal(agentId, approval.AgentId);
        Assert.Equal(toolId, approval.ToolId);
        Assert.Equal("input-hash", approval.InputHash);
        Assert.Equal(ToolExecutionApprovalStatus.Pending, approval.Status);
        Assert.Equal(expiresAtUtc, approval.ExpiresAtUtc);
        Assert.Null(approval.ApprovedAtUtc);
        Assert.Null(approval.RejectedAtUtc);
        Assert.Null(approval.ConsumedAtUtc);
    }

    [Fact]
    public void Approve_ShouldChangePendingApprovalToApproved()
    {
        var approval = CreateApproval();
        DateTime approvedAtUtc = DateTime.UtcNow;

        approval.Approve(approvedAtUtc);

        Assert.Equal(ToolExecutionApprovalStatus.Approved, approval.Status);
        Assert.Equal(approvedAtUtc, approval.ApprovedAtUtc);
    }

    [Fact]
    public void Reject_ShouldChangePendingApprovalToRejected()
    {
        var approval = CreateApproval();
        DateTime rejectedAtUtc = DateTime.UtcNow;

        approval.Reject(rejectedAtUtc);

        Assert.Equal(ToolExecutionApprovalStatus.Rejected, approval.Status);
        Assert.Equal(rejectedAtUtc, approval.RejectedAtUtc);
    }

    [Fact]
    public void Consume_ShouldAllowApprovedApprovalOnlyOnce()
    {
        var approval = CreateApproval();

        approval.Approve(DateTime.UtcNow);
        approval.Consume(DateTime.UtcNow.AddSeconds(1));

        Assert.Equal(ToolExecutionApprovalStatus.Consumed, approval.Status);
        Assert.NotNull(approval.ConsumedAtUtc);

        Assert.Throws<InvalidOperationException>(
            () => approval.Consume(DateTime.UtcNow.AddSeconds(2)));
    }

    [Fact]
    public void Expire_ShouldChangePendingApprovalToExpired()
    {
        var approval = CreateApproval();
        DateTime expiredAtUtc = DateTime.UtcNow;

        approval.Expire(expiredAtUtc);

        Assert.Equal(ToolExecutionApprovalStatus.Expired, approval.Status);
    }

    [Fact]
    public void Approve_ShouldRejectExpiredApproval()
    {
        var approval = CreateApproval(
            DateTime.UtcNow.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(
            () => approval.Approve(DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyIdentifiers()
    {
        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(10);

        Assert.Throws<ArgumentException>(
            () => new ToolExecutionApproval(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "hash",
                expiresAtUtc));

        Assert.Throws<ArgumentException>(
            () => new ToolExecutionApproval(
                Guid.NewGuid(),
                Guid.Empty,
                Guid.NewGuid(),
                "hash",
                expiresAtUtc));

        Assert.Throws<ArgumentException>(
            () => new ToolExecutionApproval(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.Empty,
                "hash",
                expiresAtUtc));
    }

    private static ToolExecutionApproval CreateApproval(
        DateTime? expiresAtUtc = null)
    {
        return new ToolExecutionApproval(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "input-hash",
            expiresAtUtc ?? DateTime.UtcNow.AddMinutes(10));
    }
}
