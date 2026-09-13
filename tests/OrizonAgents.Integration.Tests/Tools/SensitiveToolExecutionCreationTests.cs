using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class SensitiveToolExecutionCreationTests
{
    [Fact]
    public async Task NewSensitiveApproval_CreatesOneProtectedDurableExecutionAtomically()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var payloadProtector = new SensitiveToolExecutionPayloadProtector(
            new EphemeralDataProtectionProvider());
        var service = new ToolExecutionApprovalService(
            factory,
            tenant,
            new SensitiveToolExecutionFactory(payloadProtector));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        JsonElement input = Json("""{"amount":100,"account":"test-only-sensitive-payload"}""");

        ToolExecutionAuthorizationResult result = await service.AuthorizeAsync(
            binding.AgentId,
            tool,
            binding,
            input);

        Assert.Equal(ToolExecutionAuthorizationStatus.ApprovalRequired, result.Status);
        ToolExecutionApproval approval = await db.ToolExecutionApprovals.SingleAsync();
        SensitiveToolExecution execution = await db.SensitiveToolExecutions.SingleAsync();
        Assert.Equal(approval.Id, execution.ApprovalId);
        Assert.Equal(tenantId, execution.TenantId);
        Assert.Equal(binding.AgentId, execution.AgentId);
        Assert.Equal(tool.Id, execution.ToolId);
        Assert.Equal(binding.Id, execution.AgentToolBindingId);
        Assert.Equal(tool.Kind, execution.ToolKind);
        Assert.Null(execution.IntegrationConnectionId);
        Assert.Equal(SensitiveToolExecutionState.AwaitingApproval, execution.State);
        Assert.DoesNotContain("test-only-sensitive-payload", execution.ProtectedArguments);
        Assert.NotEqual(
            ToolExecutionInputHasher.Compute(input),
            execution.InputFingerprint);
        Assert.Equal(
            ToolExecutionInputHasher.Canonicalize(input),
            await payloadProtector.UnprotectAsync(
                tenantId,
                execution.Id,
                execution.ProtectedArguments));
        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(() =>
            payloadProtector.UnprotectAsync(
                Guid.NewGuid(),
                execution.Id,
                execution.ProtectedArguments));
    }

    [Fact]
    public async Task ExistingPendingApproval_DoesNotCreateAnotherDurableExecution()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var service = new ToolExecutionApprovalService(
            factory,
            tenant,
            new SensitiveToolExecutionFactory(
                new SensitiveToolExecutionPayloadProtector(
                    new EphemeralDataProtectionProvider())));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        JsonElement input = Json("""{"amount":100}""");

        ToolExecutionAuthorizationResult first = await service.AuthorizeAsync(
            binding.AgentId, tool, binding, input);
        ToolExecutionAuthorizationResult second = await service.AuthorizeAsync(
            binding.AgentId, tool, binding, input);

        Assert.Equal(first.ApprovalId, second.ApprovalId);
        Assert.Equal(1, await db.ToolExecutionApprovals.CountAsync());
        Assert.Equal(1, await db.SensitiveToolExecutions.CountAsync());
    }

    [Fact]
    public async Task OversizedArguments_FailBeforePersistingApprovalOrExecution()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var service = new ToolExecutionApprovalService(
            factory,
            tenant,
            new SensitiveToolExecutionFactory(
                new SensitiveToolExecutionPayloadProtector(
                    new EphemeralDataProtectionProvider())));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        JsonElement input = Json($"{{\"value\":\"{new string('x', 9000)}\"}}");

        await Assert.ThrowsAsync<ArgumentException>(() => service.AuthorizeAsync(
            binding.AgentId, tool, binding, input));

        Assert.Empty(db.ToolExecutionApprovals);
        Assert.Empty(db.SensitiveToolExecutions);
        Assert.DoesNotContain(
            db.ChangeTracker.Entries(),
            entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task InvalidSchema_FailsBeforeTrackingApprovalOrExecution()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var service = new ToolExecutionApprovalService(
            factory,
            tenant,
            new SensitiveToolExecutionFactory(
                new SensitiveToolExecutionPayloadProtector(
                    new EphemeralDataProtectionProvider())));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        tool.Update(tool.Name, tool.Description, tool.Endpoint, tool.HttpMethod,
            "{ invalid", tool.ToolCredentialId, tool.RiskLevel);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AuthorizeAsync(binding.AgentId, tool, binding, Json("""{"amount":100}""")));

        Assert.DoesNotContain("{ invalid", exception.Message);
        Assert.DoesNotContain(
            db.ChangeTracker.Entries(),
            entry => entry.State == EntityState.Added);
    }

    [Fact]
    public void ToolConfigurationFingerprint_TracksSemanticChangesButNotCredentialRotation()
    {
        Guid tenantId = Guid.NewGuid();
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);

        string original = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);
        tool.SetCredential(Guid.NewGuid());
        string afterCredentialRotation = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);
        tool.Update(
            tool.Name,
            tool.Description,
            "https://example.com/other-operation",
            tool.HttpMethod,
            tool.InputSchema,
            tool.ToolCredentialId,
            tool.RiskLevel);
        string afterEndpointChange = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);

        Assert.Equal(original, afterCredentialRotation);
        Assert.NotEqual(original, afterEndpointChange);
    }

    [Fact]
    public void ToolConfigurationFingerprint_CanonicalizesEquivalentSchemas()
    {
        Guid tenantId = Guid.NewGuid();
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        tool.Update(tool.Name, tool.Description, tool.Endpoint, tool.HttpMethod,
            "{ \"properties\": { \"b\": { \"type\": \"string\" }, \"a\": { \"type\": \"number\" } }, \"type\": \"object\" }",
            tool.ToolCredentialId, tool.RiskLevel);

        string first = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);
        tool.Update(tool.Name, tool.Description, tool.Endpoint, tool.HttpMethod,
            "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"number\"},\"b\":{\"type\":\"string\"}}}",
            tool.ToolCredentialId, tool.RiskLevel);
        string second = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ToolConfigurationFingerprint_PreservesArrayOrder_AndRejectsInvalidSchema()
    {
        Guid tenantId = Guid.NewGuid();
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        tool.Update(tool.Name, tool.Description, tool.Endpoint, tool.HttpMethod,
            "{\"required\":[\"a\",\"b\"]}", tool.ToolCredentialId, tool.RiskLevel);
        string first = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);
        tool.Update(tool.Name, tool.Description, tool.Endpoint, tool.HttpMethod,
            "{\"required\":[\"b\",\"a\"]}", tool.ToolCredentialId, tool.RiskLevel);
        string second = SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            tenantId, binding.AgentId, binding, tool);
        Assert.NotEqual(first, second);

        tool.Update(tool.Name, tool.Description, tool.Endpoint, tool.HttpMethod,
            "{ invalid", tool.ToolCredentialId, tool.RiskLevel);
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
                tenantId, binding.AgentId, binding, tool));
        Assert.DoesNotContain("{ invalid", exception.Message);
    }

    [Fact]
    public async Task LegacyApproval_IsExpiredAndReplacedInsteadOfBeingPromoted()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var payloadProtector = new SensitiveToolExecutionPayloadProtector(
            new EphemeralDataProtectionProvider());
        var service = new ToolExecutionApprovalService(
            factory, tenant, new SensitiveToolExecutionFactory(payloadProtector));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        JsonElement input = Json("""{"amount":100}""");
        var legacyApproval = new ToolExecutionApproval(
            tenantId, binding.AgentId, tool.Id,
            ToolExecutionInputHasher.Compute(input), DateTime.UtcNow.AddMinutes(5));
        db.ToolExecutionApprovals.Add(legacyApproval);
        await db.SaveChangesAsync();

        ToolExecutionAuthorizationResult result = await service.AuthorizeAsync(
            binding.AgentId, tool, binding, input);

        Assert.NotEqual(legacyApproval.Id, result.ApprovalId);
        await using OrizonAgentsDbContext verificationDb = factory.CreateDbContext();
        ToolExecutionApproval persistedLegacyApproval =
            await verificationDb.ToolExecutionApprovals.SingleAsync(x => x.Id == legacyApproval.Id);
        Assert.Equal(ToolExecutionApprovalStatus.Expired, persistedLegacyApproval.Status);
        Assert.Equal(2, await verificationDb.ToolExecutionApprovals.CountAsync());
        Assert.Single(verificationDb.SensitiveToolExecutions);
    }

    [Theory]
    [InlineData(ToolExecutionApprovalStatus.Pending)]
    [InlineData(ToolExecutionApprovalStatus.Approved)]
    public async Task ExpiredOpenApproval_DoesNotBlockANewDurableOperation(
        ToolExecutionApprovalStatus status)
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var service = new ToolExecutionApprovalService(
            factory,
            tenant,
            new SensitiveToolExecutionFactory(
                new SensitiveToolExecutionPayloadProtector(
                    new EphemeralDataProtectionProvider())));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var binding = new AgentToolBinding(tenantId, Guid.NewGuid(), tool.Id);
        JsonElement input = Json("""{"amount":100}""");
        var expired = new ToolExecutionApproval(
            tenantId,
            binding.AgentId,
            tool.Id,
            ToolExecutionInputHasher.Compute(input),
            DateTime.UtcNow.AddMinutes(-1));
        if (status == ToolExecutionApprovalStatus.Approved)
        {
            expired.Approve(DateTime.UtcNow.AddMinutes(-2));
        }

        db.ToolExecutionApprovals.Add(expired);
        await db.SaveChangesAsync();

        ToolExecutionAuthorizationResult result = await service.AuthorizeAsync(
            binding.AgentId, tool, binding, input);

        Assert.NotEqual(expired.Id, result.ApprovalId);
        await using OrizonAgentsDbContext verificationDb = factory.CreateDbContext();
        ToolExecutionApproval persistedExpiredApproval =
            await verificationDb.ToolExecutionApprovals.SingleAsync(x => x.Id == expired.Id);
        Assert.Equal(ToolExecutionApprovalStatus.Expired, persistedExpiredApproval.Status);
        Assert.Equal(2, await verificationDb.ToolExecutionApprovals.CountAsync());
        Assert.Single(verificationDb.SensitiveToolExecutions);
    }

    [Fact]
    public async Task MismatchedTenantBinding_FailsBeforePersistingApprovalOrExecution()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using var db = factory.CreateDbContext();
        var service = new ToolExecutionApprovalService(
            factory,
            tenant,
            new SensitiveToolExecutionFactory(
                new SensitiveToolExecutionPayloadProtector(
                    new EphemeralDataProtectionProvider())));
        AgentTool tool = CreateSensitiveHttpTool(tenantId);
        var crossTenantBinding = new AgentToolBinding(
            Guid.NewGuid(),
            Guid.NewGuid(),
            tool.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AuthorizeAsync(
            crossTenantBinding.AgentId,
            tool,
            crossTenantBinding,
            Json("""{"amount":100}""")));

        Assert.Empty(db.ToolExecutionApprovals);
        Assert.Empty(db.SensitiveToolExecutions);
    }

    private static AgentTool CreateSensitiveHttpTool(Guid tenantId)
    {
        var tool = new AgentTool(
            tenantId,
            "Sensitive operation",
            "Creates a durable operation in tests.",
            "https://example.com/operation",
            "POST");
        tool.SetRiskLevel(AgentToolRiskLevel.Sensitive);
        return tool;
    }

    private static TestDbContextFactory CreateFactory(CurrentTenant tenant) =>
        new(
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase($"SensitiveToolExecutionCreation-{Guid.NewGuid()}")
                .Options,
            tenant);

    private sealed class TestDbContextFactory(
        DbContextOptions<OrizonAgentsDbContext> options,
        CurrentTenant tenant) : IDbContextFactory<OrizonAgentsDbContext>
    {
        public OrizonAgentsDbContext CreateDbContext() => new(options, tenant);

        public Task<OrizonAgentsDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static JsonElement Json(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
