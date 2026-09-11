using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Validation;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class SensitiveToolExecutionRunner : ISensitiveToolExecutionRunner
{
    private OrizonAgentsDbContext _dbContext = null!;
    private readonly IDbContextFactory<OrizonAgentsDbContext> _dbContextFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISensitiveToolExecutionPayloadProtector _payloadProtector;
    private readonly IAgentToolInputValidator _inputValidator;
    private readonly HttpAgentToolExecutor _httpExecutor;
    private readonly GmailAgentToolExecutor _gmailExecutor;
    private readonly CalendarAgentToolExecutor? _calendarExecutor;

    public SensitiveToolExecutionRunner(
        IDbContextFactory<OrizonAgentsDbContext> dbContextFactory,
        ICurrentTenant currentTenant,
        ISensitiveToolExecutionPayloadProtector payloadProtector,
        IAgentToolInputValidator inputValidator,
        HttpAgentToolExecutor httpExecutor,
        GmailAgentToolExecutor gmailExecutor,
        CalendarAgentToolExecutor? calendarExecutor = null)
    {
        _dbContextFactory = dbContextFactory;
        _currentTenant = currentTenant;
        _payloadProtector = payloadProtector;
        _inputValidator = inputValidator;
        _httpExecutor = httpExecutor;
        _gmailExecutor = gmailExecutor;
        _calendarExecutor = calendarExecutor;
    }


    public async Task<SensitiveToolExecutionRunResult> RunAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            return await RunIsolatedAsync(executionId, cancellationToken);
        }

        return await RunWithContextAsync(_dbContext, executionId, cancellationToken);
    }

    private async Task<SensitiveToolExecutionRunResult> RunIsolatedAsync(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        AcquiredSensitiveToolExecution? acquired;

        await using (OrizonAgentsDbContext context =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            acquired = await AcquireAsync(context, executionId, cancellationToken);
        }

        if (acquired is null)
        {
            return SensitiveToolExecutionRunResult.NotAvailable();
        }

        return await ExecuteAndFinalizeAsync(acquired, cancellationToken, isolated: true);
    }

    private async Task<SensitiveToolExecutionRunResult> RunWithContextAsync(
        OrizonAgentsDbContext dbContext,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        AcquiredSensitiveToolExecution? acquired =
            await AcquireAsync(dbContext, executionId, cancellationToken);

        if (acquired is null)
        {
            return SensitiveToolExecutionRunResult.NotAvailable();
        }

        return await ExecuteAndFinalizeAsync(acquired, cancellationToken, isolated: false);
    }

    private async Task<AcquiredSensitiveToolExecution?> AcquireAsync(
        OrizonAgentsDbContext dbContext,
        Guid executionId,
        CancellationToken cancellationToken)
    {

        if (executionId == Guid.Empty)
        {
            return null;
        }

        EnsureCurrentTenant();

        SensitiveToolExecution? execution =
            await dbContext.SensitiveToolExecutions
                .Include(x => x.Approval)
                .SingleOrDefaultAsync(x => x.Id == executionId, cancellationToken);

        if (execution is null)
        {
            return null;
        }

        ResolvedSensitiveToolExecution? resolved =
            await ResolveAsync(dbContext, execution, cancellationToken);

        if (resolved is null ||
            execution.State != SensitiveToolExecutionState.Ready ||
            execution.Approval?.Status != ToolExecutionApprovalStatus.Approved ||
            execution.ExpiresAtUtc <= DateTime.UtcNow ||
            execution.Approval.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return null;
        }

        bool matchingFingerprints;
        try
        {
            matchingFingerprints = HasMatchingFingerprints(
                execution,
                resolved.Binding,
                resolved.Tool);
        }
        catch (Exception exception) when (
            exception is ArgumentException or JsonException)
        {
            return null;
        }

        if (!matchingFingerprints)
        {
            return null;
        }

        JsonElement input;
        try
        {
            input = await ReadInputAsync(execution, cancellationToken);
        }
        catch (Exception exception) when (
            exception is ArgumentException or CryptographicException or JsonException)
        {
            return null;
        }

        if (!_inputValidator.Validate(resolved.Tool.InputSchema, input).IsValid)
        {
            return null;
        }

        try
        {
            DateTime utcNow = DateTime.UtcNow;
            execution.BeginExecution(utcNow);
            execution.Approval.Consume(utcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return new AcquiredSensitiveToolExecution(execution.Id, execution.AgentId, resolved.Tool, input);
    }

    private async Task<SensitiveToolExecutionRunResult> ExecuteAndFinalizeAsync(
        AcquiredSensitiveToolExecution acquired,
        CancellationToken cancellationToken,
        bool isolated)
    {

        try
        {
            AgentToolExecutionResult result = await ExecuteAsync(
                acquired.Tool,
                acquired.AgentId,
                acquired.Input,
                cancellationToken);

            return result.Succeeded
                ? await FinalizeAsync(acquired.ExecutionId, completed: true, cancellationToken, isolated)
                : await FinalizeAsync(acquired.ExecutionId, completed: false, cancellationToken, isolated);
        }
        catch (Exception)
        {
            return await FinalizeAsync(acquired.ExecutionId, completed: false, CancellationToken.None, isolated);
        }
    }

    private async Task<ResolvedSensitiveToolExecution?> ResolveAsync(
        OrizonAgentsDbContext dbContext,
        SensitiveToolExecution execution,
        CancellationToken cancellationToken)
    {
        ToolExecutionApproval? approval = execution.Approval;
        if (approval is null ||
            approval.TenantId != execution.TenantId ||
            approval.Id != execution.ApprovalId ||
            approval.AgentId != execution.AgentId ||
            approval.ToolId != execution.ToolId)
        {
            return null;
        }

        AgentTool? tool = await dbContext.AgentTools
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == execution.ToolId, cancellationToken);
        AgentToolBinding? binding = await dbContext.AgentToolBindings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == execution.AgentToolBindingId, cancellationToken);

        bool agentExists = await dbContext.AiAgents
            .AsNoTracking()
            .AnyAsync(x => x.Id == execution.AgentId, cancellationToken);

        if (tool is null || binding is null || !agentExists ||
            tool.TenantId != execution.TenantId ||
            binding.TenantId != execution.TenantId ||
            binding.AgentId != execution.AgentId ||
            binding.ToolId != execution.ToolId ||
            tool.Kind != execution.ToolKind ||
            tool.IntegrationConnectionId != execution.IntegrationConnectionId ||
            tool.RiskLevel != AgentToolRiskLevel.Sensitive ||
            !tool.IsActive || !binding.IsActive)
        {
            return null;
        }

        if (GmailToolPolicy.IsGmail(tool.Kind) || CalendarToolPolicy.IsCalendar(tool.Kind))
        {
            if (!tool.IntegrationConnectionId.HasValue)
            {
                return null;
            }

            IntegrationConnection? connection = await dbContext.IntegrationConnections
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == tool.IntegrationConnectionId.Value, cancellationToken);

            if (connection is null ||
                connection.TenantId != execution.TenantId ||
                !connection.IsActive ||
                connection.Status != IntegrationConnectionStatus.Connected ||
                connection.Provider != IntegrationProvider.Gmail)
            {
                return null;
            }
        }

        return new ResolvedSensitiveToolExecution(tool, binding);
    }

    private static bool HasMatchingFingerprints(
        SensitiveToolExecution execution,
        AgentToolBinding binding,
        AgentTool tool) =>
        SensitiveToolExecutionFingerprint.ComputeProtectedArguments(
            execution.ProtectedArguments) == execution.InputFingerprint &&
        SensitiveToolExecutionFingerprint.ComputeToolConfiguration(
            execution.TenantId,
            execution.AgentId,
            binding,
            tool) == execution.ToolConfigurationFingerprint;

    private async Task<JsonElement> ReadInputAsync(
        SensitiveToolExecution execution,
        CancellationToken cancellationToken)
    {
        string canonicalArguments = await _payloadProtector.UnprotectAsync(
            execution.TenantId,
            execution.Id,
            execution.ProtectedArguments,
            cancellationToken);

        using JsonDocument document = JsonDocument.Parse(canonicalArguments);
        JsonElement input = document.RootElement.Clone();

        if (ToolExecutionInputHasher.Canonicalize(input) != canonicalArguments)
        {
            throw new JsonException("Persisted Tool arguments are not canonical.");
        }

        return input;
    }

    private Task<AgentToolExecutionResult> ExecuteAsync(
        AgentTool tool,
        Guid agentId,
        JsonElement input,
        CancellationToken cancellationToken) => tool.Kind switch
    {
        AgentToolKind.Http => _httpExecutor.ExecuteAsync(
            tool,
            new AgentToolExecutionRequest(agentId, tool.Id, input),
            cancellationToken),

        AgentToolKind.GmailSearch or
        AgentToolKind.GmailReadMessage or
        AgentToolKind.GmailCreateDraft or
        AgentToolKind.GmailSend or
        AgentToolKind.GmailReply => _gmailExecutor.ExecuteAsync(
            tool,
            input,
            cancellationToken),

        AgentToolKind.CalendarSearch or
        AgentToolKind.CalendarReadEvent or
        AgentToolKind.CalendarCreateEvent or
        AgentToolKind.CalendarUpdateEvent or
        AgentToolKind.CalendarDeleteEvent when _calendarExecutor is not null =>
            _calendarExecutor.ExecuteAsync(tool, input, cancellationToken),

        _ => Task.FromResult(AgentToolExecutionResult.Failure(
            "O tipo configurado para a Tool não é suportado."))
    };

    private async Task<SensitiveToolExecutionRunResult> FinalizeAsync(
        Guid executionId,
        bool completed,
        CancellationToken cancellationToken,
        bool isolated)
    {
        if (isolated)
        {
            await using OrizonAgentsDbContext context =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await FinalizeWithContextAsync(
                context,
                executionId,
                completed,
                cancellationToken);
        }

        return await FinalizeWithContextAsync(
            _dbContext,
            executionId,
            completed,
            cancellationToken);
    }

    private static async Task<SensitiveToolExecutionRunResult> FinalizeWithContextAsync(
        OrizonAgentsDbContext dbContext,
        Guid executionId,
        bool completed,
        CancellationToken cancellationToken)
    {
        SensitiveToolExecution? execution = await dbContext.SensitiveToolExecutions
            .SingleOrDefaultAsync(x => x.Id == executionId, cancellationToken);

        if (execution is null || execution.State != SensitiveToolExecutionState.Executing)
        {
            return SensitiveToolExecutionRunResult.OutcomeUnknown();
        }

        try
        {
            if (completed)
            {
                execution.Complete(DateTime.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
                return SensitiveToolExecutionRunResult.Executed();
            }

            execution.MarkOutcomeUnknown(DateTime.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another actor changed the state after the external call. Do not retry.
        }

        return SensitiveToolExecutionRunResult.OutcomeUnknown();
    }

    private void EnsureCurrentTenant()
    {
        if (!_currentTenant.HasTenant)
        {
            throw new InvalidOperationException(
                "Não há tenant ativo para executar a operação sensível.");
        }
    }

    private sealed record ResolvedSensitiveToolExecution(
        AgentTool Tool,
        AgentToolBinding Binding);

    private sealed record AcquiredSensitiveToolExecution(
        Guid ExecutionId,
        Guid AgentId,
        AgentTool Tool,
        JsonElement Input);
}
