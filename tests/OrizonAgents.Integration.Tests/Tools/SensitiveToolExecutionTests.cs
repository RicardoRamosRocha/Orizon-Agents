using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class SensitiveToolExecutionTests
{
    [Fact]
    public void RelationalModel_MapsTenantFilter_OneToOneApproval_AndConcurrencyStamp()
    {
        var tenant = new CurrentTenant();
        tenant.SetTenantId(Guid.NewGuid());

        using var db = new OrizonAgentsDbContext(
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseNpgsql(OrizonAgents.Integration.Tests.TestDatabaseConnection.For("model_tests"))
                .Options,
            tenant);

        var entity = db.Model.FindEntityType(typeof(SensitiveToolExecution))!;
        Assert.NotNull(entity.GetQueryFilter());
        Assert.True(entity.FindProperty(nameof(SensitiveToolExecution.ConcurrencyStamp))!.IsConcurrencyToken);
        Assert.Equal(
            SensitiveToolExecution.ProtectedArgumentsMaxLength,
            entity.FindProperty(nameof(SensitiveToolExecution.ProtectedArguments))!.GetMaxLength());

        var approvalForeignKey = Assert.Single(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(ToolExecutionApproval));
        Assert.True(approvalForeignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, approvalForeignKey.DeleteBehavior);
        Assert.Equal(
            new[] { nameof(SensitiveToolExecution.TenantId), nameof(SensitiveToolExecution.ApprovalId) },
            approvalForeignKey.Properties.Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(ToolExecutionApproval.TenantId), nameof(ToolExecutionApproval.Id) },
            approvalForeignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.True(entity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SensitiveToolExecution.TenantId),
                    nameof(SensitiveToolExecution.ApprovalId)
                })).IsUnique);

        var approvalEntity = db.Model.FindEntityType(typeof(ToolExecutionApproval))!;
        var openRequestIndex = approvalEntity.GetIndexes().Single(index =>
            index.GetDatabaseName() ==
            "IX_ToolExecutionApprovals_OpenEquivalentRequest");
        Assert.True(openRequestIndex.IsUnique);
        Assert.Equal(
            "\"Status\" IN ('Pending', 'Approved')",
            openRequestIndex.GetFilter());
    }

    [Fact]
    public async Task ProtectedArguments_RoundTrip_AreBoundToTenantAndExecution_AndNotPersistedAsPlaintext()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        await using var db = CreateDb(tenant);

        var approval = CreateApproval(tenantId);
        Guid executionId = Guid.NewGuid();
        var execution = CreateExecution(tenantId, approval, executionId);
        const string plaintext = "{\"body\":\"test-only-sensitive-payload\"}";
        var protector = new SensitiveToolExecutionPayloadProtector(
            new EphemeralDataProtectionProvider());

        string protectedArguments = await protector.ProtectAsync(
            tenantId,
            executionId,
            plaintext);
        execution = CreateExecution(tenantId, approval, executionId, protectedArguments);

        db.AddRange(approval, execution);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        SensitiveToolExecution stored = await db.SensitiveToolExecutions.SingleAsync();
        Assert.DoesNotContain("test-only-sensitive-payload", stored.ProtectedArguments);
        Assert.Equal(plaintext, await protector.UnprotectAsync(
            tenantId, stored.Id, stored.ProtectedArguments));
        await Assert.ThrowsAsync<CryptographicException>(() => protector.UnprotectAsync(
            Guid.NewGuid(), stored.Id, stored.ProtectedArguments));
        await Assert.ThrowsAsync<CryptographicException>(() => protector.UnprotectAsync(
            tenantId, Guid.NewGuid(), stored.ProtectedArguments));
    }

    [Fact]
    public async Task TenantQueryFilter_IsolatesSensitiveToolExecutions()
    {
        var tenant = new CurrentTenant();
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        tenant.SetTenantId(tenantA);
        await using var db = CreateDb(tenant);

        ToolExecutionApproval approvalA = CreateApproval(tenantA);
        ToolExecutionApproval approvalB = CreateApproval(tenantB);
        db.AddRange(
            approvalA,
            approvalB,
            CreateExecution(tenantA, approvalA),
            CreateExecution(tenantB, approvalB));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        SensitiveToolExecution visible = await db.SensitiveToolExecutions.SingleAsync();
        Assert.Equal(tenantA, visible.TenantId);
    }

    [Fact]
    public void StateTransitions_RequireTheExpectedState_AndRenewConcurrencyStamp()
    {
        DateTime now = DateTime.UtcNow;
        Guid tenantId = Guid.NewGuid();
        var execution = CreateExecution(
            tenantId,
            CreateApproval(tenantId),
            expiresAtUtc: now.AddMinutes(5));
        string initialStamp = execution.ConcurrencyStamp;

        execution.MarkReady(now);
        Assert.Equal(SensitiveToolExecutionState.Ready, execution.State);
        Assert.NotEqual(initialStamp, execution.ConcurrencyStamp);

        execution.BeginExecution(now);
        execution.Complete(now);
        Assert.Equal(SensitiveToolExecutionState.Executed, execution.State);
        Assert.NotNull(execution.ExecutionStartedAtUtc);
        Assert.NotNull(execution.CompletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => execution.BeginExecution(now));
    }

    [Fact]
    public void RejectionAndExpiration_AreTerminalStatesBeforeExecution()
    {
        DateTime now = DateTime.UtcNow;
        Guid tenantId = Guid.NewGuid();
        var rejected = CreateExecution(tenantId, CreateApproval(tenantId));
        var expired = CreateExecution(tenantId, CreateApproval(tenantId));

        rejected.Reject(now);
        expired.Expire(now);

        Assert.Equal(SensitiveToolExecutionState.Rejected, rejected.State);
        Assert.Equal(SensitiveToolExecutionState.Expired, expired.State);
        Assert.NotNull(rejected.CompletedAtUtc);
        Assert.NotNull(expired.CompletedAtUtc);
    }

    [Fact]
    public void ReadyExecution_CanExpire_AndCannotBeginAfterward()
    {
        DateTime now = DateTime.UtcNow;
        Guid tenantId = Guid.NewGuid();
        var execution = CreateExecution(
            tenantId,
            CreateApproval(tenantId),
            expiresAtUtc: now.AddMinutes(1));

        execution.MarkReady(now);
        execution.Expire(now);

        Assert.Equal(SensitiveToolExecutionState.Expired, execution.State);
        Assert.Throws<InvalidOperationException>(() => execution.BeginExecution(now));
    }

    [Fact]
    public void ExecutingAndTerminalExecutions_CannotExpire()
    {
        DateTime now = DateTime.UtcNow;
        Guid tenantId = Guid.NewGuid();
        var executing = CreateExecution(tenantId, CreateApproval(tenantId));
        executing.MarkReady(now);
        executing.BeginExecution(now);

        var executed = CreateExecution(tenantId, CreateApproval(tenantId));
        executed.MarkReady(now);
        executed.BeginExecution(now);
        executed.Complete(now);

        var failed = CreateExecution(tenantId, CreateApproval(tenantId));
        failed.MarkReady(now);
        failed.BeginExecution(now);
        failed.Fail(now);

        var unknown = CreateExecution(tenantId, CreateApproval(tenantId));
        unknown.MarkReady(now);
        unknown.BeginExecution(now);
        unknown.MarkOutcomeUnknown(now);

        var rejected = CreateExecution(tenantId, CreateApproval(tenantId));
        rejected.Reject(now);

        var expired = CreateExecution(tenantId, CreateApproval(tenantId));
        expired.Expire(now);

        Assert.Throws<InvalidOperationException>(() => executing.Expire(now));
        Assert.Throws<InvalidOperationException>(() => executed.Expire(now));
        Assert.Throws<InvalidOperationException>(() => failed.Expire(now));
        Assert.Throws<InvalidOperationException>(() => unknown.Expire(now));
        Assert.Throws<InvalidOperationException>(() => rejected.Expire(now));
        Assert.Throws<InvalidOperationException>(() => expired.Expire(now));
    }

    [Fact]
    public void CrossTenantApproval_IsRejectedByTheAggregate()
    {
        ToolExecutionApproval approval = CreateApproval(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => CreateExecution(Guid.NewGuid(), approval));
    }

    [Fact]
    public async Task PayloadLimit_AndInputFingerprintBehavior_AreEnforced()
    {
        var protector = new SensitiveToolExecutionPayloadProtector(
            new EphemeralDataProtectionProvider());

        await Assert.ThrowsAsync<ArgumentException>(() => protector.ProtectAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('x', SensitiveToolExecutionPayloadProtector.CanonicalArgumentsMaxBytes + 1)));

        using var first = JsonDocument.Parse("""{"b":2,"a":"value"}""");
        using var same = JsonDocument.Parse("""{"a":"value","b":2}""");
        using var different = JsonDocument.Parse("""{"a":"other","b":2}""");

        Assert.Equal(
            ToolExecutionInputHasher.Compute(first.RootElement),
            ToolExecutionInputHasher.Compute(same.RootElement));
        Assert.NotEqual(
            ToolExecutionInputHasher.Compute(first.RootElement),
            ToolExecutionInputHasher.Compute(different.RootElement));
    }

    private static OrizonAgentsDbContext CreateDb(CurrentTenant tenant) =>
        new(new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseInMemoryDatabase($"SensitiveToolExecutions-{Guid.NewGuid()}")
            .Options, tenant);

    private static ToolExecutionApproval CreateApproval(Guid tenantId) =>
        new(tenantId, Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), DateTime.UtcNow.AddMinutes(10));

    private static SensitiveToolExecution CreateExecution(
        Guid tenantId,
        ToolExecutionApproval approval,
        Guid? executionId = null,
        string protectedArguments = "protected-value",
        DateTime? expiresAtUtc = null)
    {
        var execution = new SensitiveToolExecution(
            executionId ?? Guid.NewGuid(),
            tenantId,
            approval,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            AgentToolKind.GmailSend,
            Guid.NewGuid(),
            protectedArguments,
            new string('B', 64),
            new string('C', 64),
            expiresAtUtc ?? DateTime.UtcNow.AddMinutes(10));

        return execution;
    }
}
