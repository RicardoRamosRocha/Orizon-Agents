using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class ToolExecutionApprovalServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_UsesIsolatedContext_AndDoesNotSaveTrackedConversationMessage()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantId = SetTenant(provider);
        OrizonAgentsDbContext requestDb = provider.GetRequiredService<OrizonAgentsDbContext>();

        var conversation = new AiConversation(tenantId, Guid.NewGuid());
        AiConversationMessage message = conversation.AddUserMessage("Mensagem pendente do runner.");
        requestDb.AiConversations.Add(conversation);
        await requestDb.SaveChangesAsync();

        requestDb.Entry(message).State = EntityState.Modified;

        await using (OrizonAgentsDbContext externalDb = provider
            .GetRequiredService<IDbContextFactory<OrizonAgentsDbContext>>()
            .CreateDbContext())
        {
            AiConversationMessage persistedMessage = await externalDb.AiConversationMessages
                .SingleAsync(x => x.Id == message.Id);
            externalDb.Remove(persistedMessage);
            await externalDb.SaveChangesAsync();
        }

        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionAuthorizationResult result = await CreateService(provider).AuthorizeAsync(
            Guid.NewGuid(), tool, Parse("""{"value":"test"}"""));

        Assert.Equal(ToolExecutionAuthorizationStatus.ApprovalRequired, result.Status);
        Assert.Equal(EntityState.Modified, requestDb.Entry(message).State);
        Assert.Single(await requestDb.ToolExecutionApprovals.ToListAsync());
        Assert.Single(await requestDb.SensitiveToolExecutions.ToListAsync());
    }

    [Fact]
    public async Task AuthorizeAsync_ReadTool_IsAllowedImmediately()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantId,
            AgentToolRiskLevel.Read);

        ToolExecutionApprovalService service =
            CreateService(provider);

        ToolExecutionAuthorizationResult result =
            await service.AuthorizeAsync(
                Guid.NewGuid(),
                tool,
                Parse("""{"value":"test"}"""));

        Assert.Equal(
            ToolExecutionAuthorizationStatus.Allowed,
            result.Status);

        Assert.Null(result.ApprovalId);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Assert.Empty(db.ToolExecutionApprovals);
    }

    [Fact]
    public async Task AuthorizeAsync_SensitiveTool_CreatesPendingApproval()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantId,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        JsonElement input =
            Parse("""{"amount":100,"account":"ABC"}""");

        Guid agentId = Guid.NewGuid();

        ToolExecutionAuthorizationResult result =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            result.Status);

        Assert.NotNull(result.ApprovalId);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        ToolExecutionApproval approval =
            await db.ToolExecutionApprovals.SingleAsync();

        Assert.Equal(result.ApprovalId, approval.Id);
        Assert.Equal(tenantId, approval.TenantId);
        Assert.Equal(agentId, approval.AgentId);
        Assert.Equal(tool.Id, approval.ToolId);
        Assert.Equal(
            ToolExecutionApprovalStatus.Pending,
            approval.Status);

        Assert.False(string.IsNullOrWhiteSpace(approval.InputHash));
        Assert.Equal(64, approval.InputHash.Length);
    }

    [Fact]
    public async Task AuthorizeAsync_SamePendingRequest_ReusesApproval()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantId,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        Guid agentId = Guid.NewGuid();

        JsonElement firstInput =
            Parse("""{"date":"2026-09-03","limit":10}""");

        JsonElement reorderedInput =
            Parse("""{"limit":10,"date":"2026-09-03"}""");

        ToolExecutionAuthorizationResult first =
            await service.AuthorizeAsync(
                agentId,
                tool,
                firstInput);

        ToolExecutionAuthorizationResult second =
            await service.AuthorizeAsync(
                agentId,
                tool,
                reorderedInput);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            first.Status);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            second.Status);

        Assert.Equal(first.ApprovalId, second.ApprovalId);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        Assert.Equal(
            1,
            await db.ToolExecutionApprovals.CountAsync());
    }

    [Fact]
    public async Task ApprovedRequest_IsAllowedOnceAndConsumed()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantId,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        Guid agentId = Guid.NewGuid();

        JsonElement input =
            Parse("""{"amount":100,"account":"ABC"}""");

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.NotNull(pending.ApprovalId);

        bool approved =
            await service.ApproveAsync(
                pending.ApprovalId.Value);

        Assert.True(approved);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        SensitiveToolExecution execution =
            await db.SensitiveToolExecutions.SingleAsync(x =>
                x.ApprovalId == pending.ApprovalId.Value);

        Assert.Equal(SensitiveToolExecutionState.Ready, execution.State);

        ToolExecutionAuthorizationResult allowed =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.Allowed,
            allowed.Status);

        ToolExecutionApproval consumed =
            await db.ToolExecutionApprovals
                .SingleAsync(x =>
                    x.Id == pending.ApprovalId.Value);

        Assert.Equal(
            ToolExecutionApprovalStatus.Consumed,
            consumed.Status);

        Assert.NotNull(consumed.ConsumedAtUtc);

        ToolExecutionAuthorizationResult nextAttempt =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            nextAttempt.Status);

        Assert.NotEqual(
            pending.ApprovalId,
            nextAttempt.ApprovalId);

        Assert.Equal(
            2,
            await db.ToolExecutionApprovals.CountAsync());
    }

    [Fact]
    public async Task ApprovedRequest_WithChangedInput_RequiresNewApproval()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantId,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        Guid agentId = Guid.NewGuid();

        JsonElement approvedInput =
            Parse("""{"amount":100,"account":"ABC"}""");

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(
                agentId,
                tool,
                approvedInput);

        Assert.NotNull(pending.ApprovalId);

        Assert.True(
            await service.ApproveAsync(
                pending.ApprovalId.Value));

        JsonElement changedInput =
            Parse("""{"amount":1000,"account":"ABC"}""");

        ToolExecutionAuthorizationResult changed =
            await service.AuthorizeAsync(
                agentId,
                tool,
                changedInput);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            changed.Status);

        Assert.NotEqual(
            pending.ApprovalId,
            changed.ApprovalId);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        ToolExecutionApproval original =
            await db.ToolExecutionApprovals
                .SingleAsync(x =>
                    x.Id == pending.ApprovalId.Value);

        Assert.Equal(
            ToolExecutionApprovalStatus.Approved,
            original.Status);

        Assert.Equal(
            2,
            await db.ToolExecutionApprovals.CountAsync());
    }

    [Fact]
    public async Task RejectedRequest_DoesNotAuthorizeExecution()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantId,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        Guid agentId = Guid.NewGuid();

        JsonElement input =
            Parse("""{"operation":"delete"}""");

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.NotNull(pending.ApprovalId);

        Assert.True(
            await service.RejectAsync(
                pending.ApprovalId.Value));

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        SensitiveToolExecution execution =
            await db.SensitiveToolExecutions.SingleAsync(x =>
                x.ApprovalId == pending.ApprovalId.Value);

        Assert.Equal(SensitiveToolExecutionState.Rejected, execution.State);

        ToolExecutionAuthorizationResult retry =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.ApprovalRequired,
            retry.Status);

        Assert.NotEqual(
            pending.ApprovalId,
            retry.ApprovalId);

        ToolExecutionApproval rejected =
            await db.ToolExecutionApprovals
                .SingleAsync(x =>
                    x.Id == pending.ApprovalId.Value);

        Assert.Equal(
            ToolExecutionApprovalStatus.Rejected,
            rejected.Status);
    }

    [Fact]
    public async Task AuthorizeAsync_ExpiredPendingApproval_ExpiresSensitiveExecution()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);
        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionApprovalService service = CreateService(provider);
        Guid agentId = Guid.NewGuid();
        JsonElement input = Parse("""{"operation":"transfer"}""");

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(agentId, tool, input);

        await ExpireApprovalAndExecutionAsync(provider, pending.ApprovalId!.Value);

        ToolExecutionAuthorizationResult retry =
            await service.AuthorizeAsync(agentId, tool, input);

        Assert.NotEqual(pending.ApprovalId, retry.ApprovalId);

        (ToolExecutionApproval approval, SensitiveToolExecution execution) =
            await GetApprovalAndExecutionAsync(provider, pending.ApprovalId.Value);

        Assert.Equal(ToolExecutionApprovalStatus.Expired, approval.Status);
        Assert.Equal(SensitiveToolExecutionState.Expired, execution.State);
    }

    [Fact]
    public async Task AuthorizeAsync_ExpiredApprovedApproval_ExpiresReadyExecution()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);
        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionApprovalService service = CreateService(provider);
        Guid agentId = Guid.NewGuid();
        JsonElement input = Parse("""{"operation":"transfer"}""");

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(agentId, tool, input);

        Assert.True(await service.ApproveAsync(pending.ApprovalId!.Value));

        await ExpireApprovalAndExecutionAsync(provider, pending.ApprovalId.Value);

        await service.AuthorizeAsync(agentId, tool, input);

        (ToolExecutionApproval approval, SensitiveToolExecution execution) =
            await GetApprovalAndExecutionAsync(provider, pending.ApprovalId.Value);

        Assert.Equal(ToolExecutionApprovalStatus.Expired, approval.Status);
        Assert.Equal(SensitiveToolExecutionState.Expired, execution.State);
    }

    [Fact]
    public async Task ApproveAsync_AfterExpiration_ExpiresSensitiveExecution()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);
        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionApprovalService service = CreateService(provider);

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(Guid.NewGuid(), tool, Parse("""{"operation":"delete"}"""));

        await ExpireApprovalAndExecutionAsync(provider, pending.ApprovalId!.Value);

        Assert.False(await service.ApproveAsync(pending.ApprovalId.Value));

        (ToolExecutionApproval approval, SensitiveToolExecution execution) =
            await GetApprovalAndExecutionAsync(provider, pending.ApprovalId.Value);

        Assert.Equal(ToolExecutionApprovalStatus.Expired, approval.Status);
        Assert.Equal(SensitiveToolExecutionState.Expired, execution.State);
    }

    [Fact]
    public async Task RejectAsync_AfterExpiration_ExpiresSensitiveExecution()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);
        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionApprovalService service = CreateService(provider);

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(Guid.NewGuid(), tool, Parse("""{"operation":"delete"}"""));

        await ExpireApprovalAndExecutionAsync(provider, pending.ApprovalId!.Value);

        Assert.False(await service.RejectAsync(pending.ApprovalId.Value));

        (ToolExecutionApproval approval, SensitiveToolExecution execution) =
            await GetApprovalAndExecutionAsync(provider, pending.ApprovalId.Value);

        Assert.Equal(ToolExecutionApprovalStatus.Expired, approval.Status);
        Assert.Equal(SensitiveToolExecutionState.Expired, execution.State);
    }

    [Fact]
    public async Task AuthorizeAsync_ExpiredApprovalWithTerminalExecution_ThrowsWithoutChangingExecution()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);
        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionApprovalService service = CreateService(provider);
        Guid agentId = Guid.NewGuid();
        JsonElement input = Parse("""{"operation":"transfer"}""");

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(agentId, tool, input);

        Assert.True(await service.ApproveAsync(pending.ApprovalId!.Value));

        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        SensitiveToolExecution execution = await db.SensitiveToolExecutions.SingleAsync(x =>
            x.ApprovalId == pending.ApprovalId.Value);
        DateTime utcNow = DateTime.UtcNow;
        execution.BeginExecution(utcNow);
        execution.Complete(utcNow);
        await db.SaveChangesAsync();

        await ExpireApprovalAndExecutionAsync(provider, pending.ApprovalId.Value, expireExecution: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AuthorizeAsync(agentId, tool, input));

        db.ChangeTracker.Clear();

        (ToolExecutionApproval approval, SensitiveToolExecution persistedExecution) =
            await GetApprovalAndExecutionAsync(provider, pending.ApprovalId.Value);

        Assert.Equal(ToolExecutionApprovalStatus.Approved, approval.Status);
        Assert.Equal(SensitiveToolExecutionState.Executed, persistedExecution.State);
    }

    [Fact]
    public async Task ApproveAsync_FromAnotherTenant_IsRejected()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantA = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantA,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(
                Guid.NewGuid(),
                tool,
                Parse("""{"operation":"transfer","amount":100}"""));

        Assert.NotNull(pending.ApprovalId);

        provider
            .GetRequiredService<ITenantContextSetter>()
            .SetTenantId(Guid.NewGuid());

        bool approved =
            await service.ApproveAsync(
                pending.ApprovalId.Value);

        Assert.False(approved);

        provider
            .GetRequiredService<ITenantContextSetter>()
            .SetTenantId(tenantA);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        ToolExecutionApproval approval =
            await db.ToolExecutionApprovals
                .SingleAsync(x =>
                    x.Id == pending.ApprovalId.Value);

        Assert.Equal(
            ToolExecutionApprovalStatus.Pending,
            approval.Status);

        Assert.Null(approval.ApprovedAtUtc);
    }

    [Fact]
    public async Task ApproveAndGetExecutionAsync_ReturnsReadyExecutionOnlyForTheFirstApproval()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);
        AgentTool tool = CreateTool(tenantId, AgentToolRiskLevel.Sensitive);
        ToolExecutionApprovalService service = CreateService(provider);

        ToolExecutionAuthorizationResult pending = await service.AuthorizeAsync(
            Guid.NewGuid(), tool, Parse("""{"operation":"transfer"}"""));

        ToolExecutionApprovalResult approved = await service.ApproveAndGetExecutionAsync(
            pending.ApprovalId!.Value);

        Assert.True(approved.Approved);
        Assert.NotNull(approved.ExecutionId);

        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        SensitiveToolExecution execution = await db.SensitiveToolExecutions.SingleAsync();
        Assert.Equal(approved.ExecutionId, execution.Id);
        Assert.Equal(SensitiveToolExecutionState.Ready, execution.State);

        ToolExecutionApprovalResult second = await service.ApproveAndGetExecutionAsync(
            pending.ApprovalId.Value);

        Assert.False(second.Approved);
        Assert.Null(second.ExecutionId);
    }

    [Fact]
    public async Task RejectAsync_FromAnotherTenant_IsRejected()
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantA = SetTenant(provider);

        AgentTool tool = CreateTool(
            tenantA,
            AgentToolRiskLevel.Sensitive);

        ToolExecutionApprovalService service =
            CreateService(provider);

        ToolExecutionAuthorizationResult pending =
            await service.AuthorizeAsync(
                Guid.NewGuid(),
                tool,
                Parse("""{"operation":"delete","resourceId":123}"""));

        Assert.NotNull(pending.ApprovalId);

        provider
            .GetRequiredService<ITenantContextSetter>()
            .SetTenantId(Guid.NewGuid());

        bool rejected =
            await service.RejectAsync(
                pending.ApprovalId.Value);

        Assert.False(rejected);

        provider
            .GetRequiredService<ITenantContextSetter>()
            .SetTenantId(tenantA);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        ToolExecutionApproval approval =
            await db.ToolExecutionApprovals
                .SingleAsync(x =>
                    x.Id == pending.ApprovalId.Value);

        Assert.Equal(
            ToolExecutionApprovalStatus.Pending,
            approval.Status);

        Assert.Null(approval.RejectedAtUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ApproveOrRejectAsync_WithoutSensitiveExecution_DoesNotPersistPartialDecision(
        bool approve)
    {
        await using ServiceProvider provider = CreateProvider();

        Guid tenantId = SetTenant(provider);

        var approval = new ToolExecutionApproval(
            tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "input-hash",
            DateTime.UtcNow.AddMinutes(5));

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        db.ToolExecutionApprovals.Add(approval);
        await db.SaveChangesAsync();

        ToolExecutionApprovalService service = CreateService(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => approve
            ? service.ApproveAsync(approval.Id)
            : service.RejectAsync(approval.Id));

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        ToolExecutionApproval persisted =
            await db.ToolExecutionApprovals.SingleAsync(x => x.Id == approval.Id);

        Assert.Equal(ToolExecutionApprovalStatus.Pending, persisted.Status);
        Assert.Null(persisted.ApprovedAtUtc);
        Assert.Null(persisted.RejectedAtUtc);
    }

    private static ToolExecutionApprovalService CreateService(
        ServiceProvider provider) =>
        new(
            provider.GetRequiredService<IDbContextFactory<OrizonAgentsDbContext>>(),
            provider.GetRequiredService<ICurrentTenant>(),
            new SensitiveToolExecutionFactory(
                new SensitiveToolExecutionPayloadProtector(
                    new EphemeralDataProtectionProvider())));

    private static async Task ExpireApprovalAndExecutionAsync(
        ServiceProvider provider,
        Guid approvalId,
        bool expireExecution = true)
    {
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        ToolExecutionApproval approval = await db.ToolExecutionApprovals
            .Include(x => x.SensitiveToolExecution)
            .SingleAsync(x => x.Id == approvalId);

        DateTime expiredAtUtc = DateTime.UtcNow.AddMinutes(-1);
        db.Entry(approval).Property(x => x.ExpiresAtUtc).CurrentValue = expiredAtUtc;

        if (expireExecution)
        {
            db.Entry(approval.SensitiveToolExecution!)
                .Property(x => x.ExpiresAtUtc)
                .CurrentValue = expiredAtUtc;
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task<(ToolExecutionApproval Approval, SensitiveToolExecution Execution)>
        GetApprovalAndExecutionAsync(ServiceProvider provider, Guid approvalId)
    {
        OrizonAgentsDbContext db = provider.GetRequiredService<OrizonAgentsDbContext>();
        ToolExecutionApproval approval = await db.ToolExecutionApprovals
            .Include(x => x.SensitiveToolExecution)
            .SingleAsync(x => x.Id == approvalId);

        return (approval, approval.SensitiveToolExecution!);
    }

    private static AgentTool CreateTool(
        Guid tenantId,
        AgentToolRiskLevel riskLevel)
    {
        var tool = new AgentTool(
            tenantId,
            "Tool de teste",
            "Tool utilizada pelos testes de aprovação.",
            "https://example.com/api/test",
            "POST");

        tool.SetRiskLevel(riskLevel);

        return tool;
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document =
            JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static Guid SetTenant(ServiceProvider provider)
    {
        Guid tenantId = Guid.NewGuid();

        provider
            .GetRequiredService<ITenantContextSetter>()
            .SetTenantId(tenantId);

        return tenantId;
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddScoped<CurrentTenant>();

        services.AddScoped<ICurrentTenant>(
            provider =>
                provider.GetRequiredService<CurrentTenant>());

        services.AddScoped<ITenantContextSetter>(
            provider =>
                provider.GetRequiredService<CurrentTenant>());

        services.AddDbContextFactory<OrizonAgentsDbContext>(
            options =>
                options.UseInMemoryDatabase(
                    $"ToolExecutionApprovals-{Guid.NewGuid()}"),
            ServiceLifetime.Scoped);

        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<OrizonAgentsDbContext>>()
                .CreateDbContext());

        return services.BuildServiceProvider();
    }
}

internal static class ToolExecutionApprovalServiceTestExtensions
{
    public static Task<ToolExecutionAuthorizationResult> AuthorizeAsync(
        this ToolExecutionApprovalService service,
        Guid agentId,
        AgentTool tool,
        JsonElement? input,
        CancellationToken cancellationToken = default) =>
        service.AuthorizeAsync(
            agentId,
            tool,
            new AgentToolBinding(tool.TenantId, agentId, tool.Id),
            input,
            cancellationToken);
}
