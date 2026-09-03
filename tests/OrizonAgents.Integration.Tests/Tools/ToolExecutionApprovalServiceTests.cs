using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class ToolExecutionApprovalServiceTests
{
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

        ToolExecutionAuthorizationResult allowed =
            await service.AuthorizeAsync(
                agentId,
                tool,
                input);

        Assert.Equal(
            ToolExecutionAuthorizationStatus.Allowed,
            allowed.Status);

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

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

        OrizonAgentsDbContext db =
            provider.GetRequiredService<OrizonAgentsDbContext>();

        ToolExecutionApproval rejected =
            await db.ToolExecutionApprovals
                .SingleAsync(x =>
                    x.Id == pending.ApprovalId.Value);

        Assert.Equal(
            ToolExecutionApprovalStatus.Rejected,
            rejected.Status);
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

    private static ToolExecutionApprovalService CreateService(
        ServiceProvider provider) =>
        new(
            provider.GetRequiredService<OrizonAgentsDbContext>(),
            provider.GetRequiredService<ICurrentTenant>());

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

        services.AddDbContext<OrizonAgentsDbContext>(
            options =>
                options.UseInMemoryDatabase(
                    $"ToolExecutionApprovals-{Guid.NewGuid()}"));

        return services.BuildServiceProvider();
    }
}
