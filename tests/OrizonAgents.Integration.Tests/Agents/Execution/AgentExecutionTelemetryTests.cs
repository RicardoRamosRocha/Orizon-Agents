using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Execution.Telemetry;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Execution.Telemetry;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class AgentExecutionTelemetryTests
{
    [Fact]
    public async Task CompleteAsync_PersistsOnlyAggregatedMetrics()
    {
        Guid tenantId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        var factory = new TestDbContextFactory(tenantId);
        var telemetry = new AgentExecutionTelemetry(
            factory,
            TimeProvider.System,
            NullLogger<AgentExecutionTelemetry>.Instance);

        IAgentExecutionTelemetrySession session = telemetry.Start(
            new AgentExecutionTelemetryStart(
                tenantId,
                agentId,
                "GoogleGemini",
                "gemini-test"));
        session.RecordModelCall();
        session.RecordModelUsage(new AiChatUsage(10, 4, 14));
        session.RecordModelCall();
        session.RecordModelUsage(new AiChatUsage(20, 6, 26));
        session.RecordToolExecution(succeeded: true, approvalRequired: false);
        session.RecordToolExecution(succeeded: false, approvalRequired: false);
        session.RecordToolExecution(succeeded: false, approvalRequired: true);
        session.RecordRagResults(3);
        session.RecordToolContext(1_000, 700);
        Guid conversationId = Guid.NewGuid();

        await session.CompleteAsync(conversationId, succeeded: true);

        await using OrizonAgentsDbContext context = factory.CreateDbContext();
        AgentExecutionUsage usage =
            await context.AgentExecutionUsages.SingleAsync();
        Assert.Equal(tenantId, usage.TenantId);
        Assert.Equal(agentId, usage.AgentId);
        Assert.Equal(conversationId, usage.ConversationId);
        Assert.Equal(2, usage.ModelCallCount);
        Assert.Equal(3, usage.ToolExecutionCount);
        Assert.Equal(1, usage.ToolSuccessCount);
        Assert.Equal(1, usage.ToolFailureCount);
        Assert.Equal(1, usage.ToolApprovalRequiredCount);
        Assert.Equal(3, usage.RagResultCount);
        Assert.Equal(700, usage.ToolContextCharacters);
        Assert.Equal(300, usage.ContextReductionCharacters);
        Assert.Equal(30, usage.InputTokens);
        Assert.Equal(10, usage.OutputTokens);
        Assert.Equal(40, usage.TotalTokens);
        Assert.True(usage.Succeeded);
        Assert.True(usage.DurationMs >= 0);

        string[] propertyNames = typeof(AgentExecutionUsage)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain("UserMessage", propertyNames);
        Assert.DoesNotContain("SystemPrompt", propertyNames);
        Assert.DoesNotContain("OperationalContext", propertyNames);
        Assert.DoesNotContain("ToolResult", propertyNames);
        Assert.DoesNotContain("BodyText", propertyNames);
        Assert.DoesNotContain("ErrorMessage", propertyNames);
    }

    [Fact]
    public async Task QueryFilter_IsolatesUsageByTenant()
    {
        string databaseName = Guid.NewGuid().ToString();
        Guid firstTenantId = Guid.NewGuid();
        Guid secondTenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var seed = CreateContext(options, firstTenantId))
        {
            seed.AgentExecutionUsages.Add(CreateUsage(firstTenantId));
            seed.AgentExecutionUsages.Add(CreateUsage(secondTenantId));
            await seed.SaveChangesAsync();
        }

        await using var first = CreateContext(options, firstTenantId);
        await using var second = CreateContext(options, secondTenantId);
        Assert.Equal(firstTenantId, (await first.AgentExecutionUsages.SingleAsync()).TenantId);
        Assert.Equal(secondTenantId, (await second.AgentExecutionUsages.SingleAsync()).TenantId);
    }

    private static AgentExecutionUsage CreateUsage(Guid tenantId)
    {
        DateTime now = DateTime.UtcNow;
        return new AgentExecutionUsage(
            tenantId,
            Guid.NewGuid(),
            null,
            "Provider",
            "Model",
            now,
            now,
            0,
            true,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            null);
    }

    private static OrizonAgentsDbContext CreateContext(
        DbContextOptions<OrizonAgentsDbContext> options,
        Guid tenantId)
    {
        var tenant = new CurrentTenant();
        tenant.SetTenantId(tenantId);
        return new OrizonAgentsDbContext(options, tenant);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<OrizonAgentsDbContext>
    {
        private readonly DbContextOptions<OrizonAgentsDbContext> _options;
        private readonly Guid _tenantId;

        public TestDbContextFactory(Guid tenantId)
        {
            _tenantId = tenantId;
            _options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        public OrizonAgentsDbContext CreateDbContext() =>
            CreateContext(_options, _tenantId);

        public Task<OrizonAgentsDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
