using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tools.Execution;
using OrizonAgents.Infrastructure.Tools.Validation;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class SensitiveToolExecutionRunnerTests
{
    [Fact]
    public async Task RunAsync_ReadyApprovedExecution_ExecutesOnceWithPersistedArguments()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = SetTenant(tenant);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using OrizonAgentsDbContext db = factory.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.OK, "ok");
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();

        Guid executionId = await SeedReadyExecutionAsync(db, factory, tenant, tenantId, protection,
            Json("""{"amount":100,"account":"ABC"}"""));

        SensitiveToolExecutionRunResult result = await CreateRunner(
            factory, tenant, protection, handler).RunAsync(executionId);

        Assert.Equal(SensitiveToolExecutionRunStatus.Executed, result.Status);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("{\"account\":\"ABC\",\"amount\":100}", handler.RequestBody);

        await using OrizonAgentsDbContext verificationDb = factory.CreateDbContext();
        SensitiveToolExecution execution = await verificationDb.SensitiveToolExecutions.SingleAsync();
        ToolExecutionApproval approval = await verificationDb.ToolExecutionApprovals.SingleAsync();
        Assert.Equal(SensitiveToolExecutionState.Executed, execution.State);
        Assert.Equal(ToolExecutionApprovalStatus.Consumed, approval.Status);

        SensitiveToolExecutionRunResult retry = await CreateRunner(
            factory, tenant, protection, handler).RunAsync(executionId);
        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable, retry.Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_ModifiedToolOrInactiveBinding_BlocksExecution()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = SetTenant(tenant);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using OrizonAgentsDbContext db = factory.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.OK, "ok");
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        Guid executionId = await SeedReadyExecutionAsync(db, factory, tenant, tenantId, protection, Json("""{"value":"one"}"""));

        AgentTool tool = await db.AgentTools.SingleAsync();
        tool.Update(tool.Name, tool.Description, "https://example.com/changed", "POST",
            tool.InputSchema, tool.ToolCredentialId, tool.RiskLevel);
        await db.SaveChangesAsync();

        SensitiveToolExecutionRunResult changedTool = await CreateRunner(
            factory, tenant, protection, handler).RunAsync(executionId);
        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable, changedTool.Status);

        tool.Update(tool.Name, tool.Description, "https://example.com/operation", "POST",
            tool.InputSchema, tool.ToolCredentialId, tool.RiskLevel);
        AgentToolBinding binding = await db.AgentToolBindings.SingleAsync();
        binding.Deactivate();
        await db.SaveChangesAsync();

        SensitiveToolExecutionRunResult inactiveBinding = await CreateRunner(
            factory, tenant, protection, handler).RunAsync(executionId);
        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable, inactiveBinding.Status);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(SensitiveToolExecutionState.Ready,
            (await db.SensitiveToolExecutions.SingleAsync()).State);
    }

    [Fact]
    public async Task RunAsync_ExpiredOrNonApprovedExecution_DoesNotCallExternalTool()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = SetTenant(tenant);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using OrizonAgentsDbContext db = factory.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.OK, "ok");
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        Guid executionId = await SeedReadyExecutionAsync(db, factory, tenant, tenantId, protection, Json("""{"value":"one"}"""));

        SensitiveToolExecution execution = await db.SensitiveToolExecutions.SingleAsync();
        db.Entry(execution).Property(x => x.ExpiresAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable,
            (await CreateRunner(factory, tenant, protection, handler).RunAsync(executionId)).Status);
        Assert.Equal(0, handler.CallCount);

        db.Entry(execution).Property(x => x.ExpiresAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(5);
        ToolExecutionApproval approval = await db.ToolExecutionApprovals.SingleAsync();
        db.Entry(approval).Property(x => x.Status).CurrentValue = ToolExecutionApprovalStatus.Rejected;
        await db.SaveChangesAsync();

        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable,
            (await CreateRunner(factory, tenant, protection, handler).RunAsync(executionId)).Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_TamperedPayload_DoesNotCallExternalTool()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = SetTenant(tenant);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using OrizonAgentsDbContext db = factory.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.OK, "ok");
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        Guid executionId = await SeedReadyExecutionAsync(db, factory, tenant, tenantId, protection, Json("""{"value":"one"}"""));

        SensitiveToolExecution execution = await db.SensitiveToolExecutions.SingleAsync();
        db.Entry(execution).Property(x => x.ProtectedArguments).CurrentValue = "tampered";
        await db.SaveChangesAsync();

        SensitiveToolExecutionRunResult result = await CreateRunner(
            factory, tenant, protection, handler).RunAsync(executionId);

        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_ExternalFailure_IsOutcomeUnknownAndIsNotRetried()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = SetTenant(tenant);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using OrizonAgentsDbContext db = factory.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.BadRequest, "invalid");
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        Guid executionId = await SeedReadyExecutionAsync(db, factory, tenant, tenantId, protection, Json("""{"value":"one"}"""));
        SensitiveToolExecutionRunner runner = CreateRunner(factory, tenant, protection, handler);

        Assert.Equal(SensitiveToolExecutionRunStatus.OutcomeUnknown,
            (await runner.RunAsync(executionId)).Status);
        await using OrizonAgentsDbContext verificationDb = factory.CreateDbContext();
        Assert.Equal(SensitiveToolExecutionState.OutcomeUnknown,
            (await verificationDb.SensitiveToolExecutions.SingleAsync()).State);

        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable,
            (await runner.RunAsync(executionId)).Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_FromAnotherTenant_DoesNotExecute()
    {
        var tenant = new CurrentTenant();
        Guid tenantId = SetTenant(tenant);
        TestDbContextFactory factory = CreateFactory(tenant);
        await using OrizonAgentsDbContext db = factory.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.OK, "ok");
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        Guid executionId = await SeedReadyExecutionAsync(db, factory, tenant, tenantId, protection, Json("""{"value":"one"}"""));

        tenant.SetTenantId(Guid.NewGuid());
        db.ChangeTracker.Clear();

        SensitiveToolExecutionRunResult result = await CreateRunner(
            factory, tenant, protection, handler).RunAsync(executionId);

        Assert.Equal(SensitiveToolExecutionRunStatus.NotAvailable, result.Status);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_ConcurrentAttempts_ExecuteOnlyOnce()
    {
        var root = new InMemoryDatabaseRoot();
        string databaseName = $"SensitiveToolExecutionRunner-{Guid.NewGuid()}";
        IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
        var seedTenant = new CurrentTenant();
        Guid tenantId = SetTenant(seedTenant);
        Guid executionId;

        TestDbContextFactory seedFactory = CreateFactory(seedTenant, databaseName, root);
        await using (OrizonAgentsDbContext seedDb = seedFactory.CreateDbContext())
        {
            executionId = await SeedReadyExecutionAsync(seedDb, seedFactory, seedTenant, tenantId, protection, Json("""{"value":"one"}"""));
        }

        var tenantA = new CurrentTenant();
        tenantA.SetTenantId(tenantId);
        var tenantB = new CurrentTenant();
        tenantB.SetTenantId(tenantId);
        TestDbContextFactory factoryA = CreateFactory(tenantA, databaseName, root);
        TestDbContextFactory factoryB = CreateFactory(tenantB, databaseName, root);
        await using OrizonAgentsDbContext dbA = factoryA.CreateDbContext();
        await using OrizonAgentsDbContext dbB = factoryB.CreateDbContext();
        var handler = new RecordingHandler(HttpStatusCode.OK, "ok");
        using var barrier = new Barrier(2);
        var validator = new BarrierInputValidator(barrier);

        Task<SensitiveToolExecutionRunResult> first = Task.Run(() => CreateRunner(
            factoryA, tenantA, protection, handler, validator).RunAsync(executionId));
        Task<SensitiveToolExecutionRunResult> second = Task.Run(() => CreateRunner(
            factoryB, tenantB, protection, handler, validator).RunAsync(executionId));

        SensitiveToolExecutionRunResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, x => x.Status == SensitiveToolExecutionRunStatus.Executed);
        Assert.Single(results, x => x.Status == SensitiveToolExecutionRunStatus.NotAvailable);
        Assert.Equal(1, handler.CallCount);
    }

    private static async Task<Guid> SeedReadyExecutionAsync(
        OrizonAgentsDbContext db,
        IDbContextFactory<OrizonAgentsDbContext> dbContextFactory,
        CurrentTenant tenant,
        Guid tenantId,
        IDataProtectionProvider protection,
        JsonElement input)
    {
        var agent = new AiAgent(tenantId, "Agent", "Test agent", AiProvider.GoogleGemini, "test");
        var tool = new AgentTool(tenantId, "Operation", "Sensitive test operation",
            "https://example.com/operation", "POST");
        tool.SetRiskLevel(AgentToolRiskLevel.Sensitive);
        var binding = new AgentToolBinding(tenantId, agent.Id, tool.Id);
        db.AddRange(agent, tool, binding);
        await db.SaveChangesAsync();

        var approvals = new ToolExecutionApprovalService(dbContextFactory, tenant,
            new SensitiveToolExecutionFactory(new SensitiveToolExecutionPayloadProtector(protection)));
        ToolExecutionAuthorizationResult pending = await approvals.AuthorizeAsync(agent.Id, tool, binding, input);
        await approvals.ApproveAsync(pending.ApprovalId!.Value);

        return (await db.SensitiveToolExecutions.SingleAsync()).Id;
    }

    private static SensitiveToolExecutionRunner CreateRunner(
        IDbContextFactory<OrizonAgentsDbContext> dbContextFactory,
        CurrentTenant tenant,
        IDataProtectionProvider protection,
        HttpMessageHandler handler,
        OrizonAgents.Application.Tools.Validation.IAgentToolInputValidator? inputValidator = null) => new(
            dbContextFactory,
            tenant,
            new SensitiveToolExecutionPayloadProtector(protection),
            inputValidator ?? new AgentToolInputValidator(),
            new HttpAgentToolExecutor(
                new StubHttpClientFactory(handler),
                new AllowAllEndpointPolicy(),
                new StubToolCredentialService(),
                Options.Create(new AgentToolHttpOptions()),
                NullLogger<HttpAgentToolExecutor>.Instance),
            null!);

    private static TestDbContextFactory CreateFactory(CurrentTenant tenant) =>
        new(
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase($"SensitiveToolExecutionRunner-{Guid.NewGuid()}")
                .Options,
            tenant);

    private static OrizonAgentsDbContext CreateDb(CurrentTenant tenant) =>
        CreateFactory(tenant).CreateDbContext();

    private static OrizonAgentsDbContext CreateDb(
        CurrentTenant tenant,
        string databaseName,
        InMemoryDatabaseRoot root) => new(
        new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options,
        tenant);

    private static TestDbContextFactory CreateFactory(
        CurrentTenant tenant,
        string databaseName,
        InMemoryDatabaseRoot root) =>
        new(
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase(databaseName, root)
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

    private static Guid SetTenant(CurrentTenant tenant)
    {
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        return tenantId;
    }

    private static JsonElement Json(string value)
    {
        using JsonDocument document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private sealed class AllowAllEndpointPolicy : IAgentToolEndpointPolicy
    {
        public Task<bool> IsAllowedAsync(Uri endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler);
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubToolCredentialService : IToolCredentialService
    {
        public Task<OrizonAgents.Application.Common.Results.OperationResult<Guid>> CreateAsync(OrizonAgents.Application.Tools.Requests.CreateToolCredentialRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrizonAgents.Application.Tools.Models.ToolCredentialListItemDto>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OrizonAgents.Application.Common.Results.OperationResult> RotateSecretAsync(Guid credentialId, string secret, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResolvedToolCredential?> ResolveForExecutionAsync(Guid credentialId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<ResolvedToolCredential?>(null);
        public Task<OrizonAgents.Application.Common.Results.OperationResult> SetActiveAsync(Guid credentialId, bool active, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode) { Content = new StringContent(response, Encoding.UTF8) };
        }
    }

    private sealed class BarrierInputValidator(Barrier barrier) : OrizonAgents.Application.Tools.Validation.IAgentToolInputValidator
    {
        public OrizonAgents.Application.Tools.Validation.AgentToolInputValidationResult Validate(
            string? inputSchema,
            JsonElement? input)
        {
            barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            return OrizonAgents.Application.Tools.Validation.AgentToolInputValidationResult.Success();
        }
    }
}
