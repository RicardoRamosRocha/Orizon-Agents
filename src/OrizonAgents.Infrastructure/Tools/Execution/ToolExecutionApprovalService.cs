using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class ToolExecutionApprovalService
    : IToolExecutionApprovalService
{
    private static readonly TimeSpan ApprovalLifetime =
        TimeSpan.FromMinutes(10);

    private OrizonAgentsDbContext _dbContext = null!;
    private readonly IDbContextFactory<OrizonAgentsDbContext> _dbContextFactory;
    private readonly ICurrentTenant _currentTenant;
    private const string OpenEquivalentRequestIndexName =
        "IX_ToolExecutionApprovals_OpenEquivalentRequest";

    private readonly ISensitiveToolExecutionFactory? _executionFactory;

    public ToolExecutionApprovalService(
        IDbContextFactory<OrizonAgentsDbContext> dbContextFactory,
        ICurrentTenant currentTenant,
        ISensitiveToolExecutionFactory? executionFactory = null)
    {
        _dbContextFactory = dbContextFactory;
        _currentTenant = currentTenant;
        _executionFactory = executionFactory;
    }


    public async Task<IReadOnlyList<ToolExecutionApprovalListItemDto>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            await using OrizonAgentsDbContext context =
                await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await CreateOperationService(context).ListPendingAsync(cancellationToken);
        }

        EnsureCurrentTenant();

        DateTime utcNow = DateTime.UtcNow;

        return await (
            from approval in _dbContext.ToolExecutionApprovals
            join agent in _dbContext.AiAgents
                on approval.AgentId equals agent.Id
            join tool in _dbContext.AgentTools
                on approval.ToolId equals tool.Id
            where approval.Status == ToolExecutionApprovalStatus.Pending &&
                  approval.ExpiresAtUtc > utcNow
            orderby approval.CreatedAtUtc descending
            select new ToolExecutionApprovalListItemDto(
                approval.Id,
                approval.AgentId,
                agent.Name,
                approval.ToolId,
                tool.Name,
                tool.RiskLevel,
                approval.CreatedAtUtc,
                approval.ExpiresAtUtc))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ToolExecutionAuthorizationResult> AuthorizeAsync(
        Guid agentId,
        AgentTool tool,
        AgentToolBinding binding,
        JsonElement? input,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            await using OrizonAgentsDbContext context =
                await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await CreateOperationService(context).AuthorizeAsync(agentId, tool, binding, input, cancellationToken);
        }

        if (agentId == Guid.Empty)
        {
            throw new ArgumentException(
                "AgentId é obrigatório.",
                nameof(agentId));
        }

        if (tool is null)
        {
            throw new ArgumentNullException(nameof(tool));
        }

        ArgumentNullException.ThrowIfNull(binding);

        EnsureCurrentTenant(tool.TenantId);

        if (tool.RiskLevel != AgentToolRiskLevel.Sensitive)
        {
            return ToolExecutionAuthorizationResult.Allowed();
        }

        string inputHash =
            ToolExecutionInputHasher.Compute(input);

        DateTime utcNow = DateTime.UtcNow;

        ToolExecutionApproval? approval =
            await _dbContext.ToolExecutionApprovals
                .Include(x => x.SensitiveToolExecution)
                .Where(x =>
                    x.AgentId == agentId &&
                    x.ToolId == tool.Id &&
                    x.InputHash == inputHash &&
                    (x.Status == ToolExecutionApprovalStatus.Pending ||
                     x.Status == ToolExecutionApprovalStatus.Approved))
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        if (approval is not null)
        {
            if (approval.ExpiresAtUtc <= utcNow)
            {
                ExpireApprovalAndSensitiveToolExecution(approval, utcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
                approval = null;
            }
            else if (approval.SensitiveToolExecution is null)
            {
                approval.Expire(utcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
                approval = null;
            }
            else if (approval.Status == ToolExecutionApprovalStatus.Approved)
            {
                approval.Consume(utcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return ToolExecutionAuthorizationResult.Allowed();
            }
            else
            {
                return ToolExecutionAuthorizationResult.ApprovalRequired(
                    approval.Id);
            }
        }

        var pending = new ToolExecutionApproval(
            tool.TenantId,
            agentId,
            tool.Id,
            inputHash,
            utcNow.Add(ApprovalLifetime));

        SensitiveToolExecution execution =
            await (_executionFactory ?? throw new InvalidOperationException(
                "Sensitive Tool execution factory is not configured.")).CreateAsync(
                pending,
                tool,
                binding,
                input,
                cancellationToken);

        try
        {
            _dbContext.SensitiveToolExecutions.Add(execution);
            _dbContext.ToolExecutionApprovals.Add(pending);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOpenEquivalentRequestConflict(exception))
        {
            _dbContext.ChangeTracker.Clear();

            ToolExecutionApproval? winner = await FindOpenApprovalAsync(
                agentId,
                tool.Id,
                inputHash,
                cancellationToken);

            if (winner is not null &&
                winner.ExpiresAtUtc > DateTime.UtcNow &&
                winner.SensitiveToolExecution is not null)
            {
                if (winner.Status == ToolExecutionApprovalStatus.Pending)
                {
                    return ToolExecutionAuthorizationResult.ApprovalRequired(winner.Id);
                }

                if (winner.Status == ToolExecutionApprovalStatus.Approved)
                {
                    winner.Consume(DateTime.UtcNow);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return ToolExecutionAuthorizationResult.Allowed();
                }
            }

            throw;
        }

        return ToolExecutionAuthorizationResult.ApprovalRequired(
            pending.Id);
    }

    private async Task<ToolExecutionApproval?> FindOpenApprovalAsync(
        Guid agentId,
        Guid toolId,
        string inputHash,
        CancellationToken cancellationToken) =>
        await _dbContext.ToolExecutionApprovals
            .Include(x => x.SensitiveToolExecution)
            .Where(x =>
                x.AgentId == agentId &&
                x.ToolId == toolId &&
                x.InputHash == inputHash &&
                (x.Status == ToolExecutionApprovalStatus.Pending ||
                 x.Status == ToolExecutionApprovalStatus.Approved))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool IsOpenEquivalentRequestConflict(
        DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: "23505",
            ConstraintName: OpenEquivalentRequestIndexName
        };

    public async Task<bool> ApproveAsync(
        Guid approvalId,
        CancellationToken cancellationToken = default)
    {
        ToolExecutionApprovalResult result =
            await ApproveAndGetExecutionAsync(approvalId, cancellationToken);

        return result.Approved;
    }

    public async Task<ToolExecutionApprovalResult> ApproveAndGetExecutionAsync(
        Guid approvalId,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            await using OrizonAgentsDbContext context =
                await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await CreateOperationService(context).ApproveAndGetExecutionAsync(approvalId, cancellationToken);
        }

        EnsureCurrentTenant();

        ToolExecutionApproval? approval =
            await FindApprovalAsync(
                approvalId,
                cancellationToken);

        if (approval is null)
        {
            return ToolExecutionApprovalResult.NotAvailable();
        }

        DateTime utcNow = DateTime.UtcNow;

        if (approval.Status != ToolExecutionApprovalStatus.Pending ||
            approval.ExpiresAtUtc <= utcNow)
        {
            if ((approval.Status is ToolExecutionApprovalStatus.Pending or
                 ToolExecutionApprovalStatus.Approved) &&
                approval.ExpiresAtUtc <= utcNow)
            {
                ExpireApprovalAndSensitiveToolExecution(approval, utcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return ToolExecutionApprovalResult.NotAvailable();
        }

        approval.Approve(utcNow);
        SensitiveToolExecution execution =
            GetRequiredSensitiveToolExecution(approval);

        execution.MarkReady(utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToolExecutionApprovalResult.Success(execution.Id);
    }

    public async Task<bool> RejectAsync(
        Guid approvalId,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            await using OrizonAgentsDbContext context =
                await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await CreateOperationService(context).RejectAsync(approvalId, cancellationToken);
        }

        EnsureCurrentTenant();

        ToolExecutionApproval? approval =
            await FindApprovalAsync(
                approvalId,
                cancellationToken);

        if (approval is null ||
            approval.Status != ToolExecutionApprovalStatus.Pending)
        {
            return false;
        }

        DateTime utcNow = DateTime.UtcNow;

        if (approval.ExpiresAtUtc <= utcNow)
        {
            ExpireApprovalAndSensitiveToolExecution(approval, utcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        approval.Reject(utcNow);
        SensitiveToolExecution execution =
            GetRequiredSensitiveToolExecution(approval);

        execution.Reject(utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<ToolExecutionApproval?> FindApprovalAsync(
        Guid approvalId,
        CancellationToken cancellationToken)
    {
        if (approvalId == Guid.Empty)
        {
            return Task.FromResult<ToolExecutionApproval?>(null);
        }

        return _dbContext.ToolExecutionApprovals
            .Include(x => x.SensitiveToolExecution)
            .SingleOrDefaultAsync(
                x => x.Id == approvalId,
                cancellationToken);
    }

    private SensitiveToolExecution GetRequiredSensitiveToolExecution(
        ToolExecutionApproval approval)
    {
        if (approval.SensitiveToolExecution is { } execution)
        {
            return execution;
        }

        throw new InvalidOperationException(
            "Sensitive Tool execution was not found for the approval.");
    }

    private void ExpireApprovalAndSensitiveToolExecution(
        ToolExecutionApproval approval,
        DateTime utcNow)
    {
        approval.Expire(utcNow);

        SensitiveToolExecution? execution = approval.SensitiveToolExecution;

        if (execution is null)
        {
            // Approvals created before durable executions existed remain expirable.
            return;
        }

        if (execution.State is SensitiveToolExecutionState.AwaitingApproval or
            SensitiveToolExecutionState.Ready)
        {
            execution.Expire(utcNow);
            return;
        }

        throw new InvalidOperationException(
            "Sensitive Tool execution cannot be expired in its current state.");
    }

    private void EnsureCurrentTenant()
    {
        if (!_currentTenant.HasTenant)
        {
            throw new InvalidOperationException(
                "Não há tenant ativo para autorizar a execução da Tool.");
        }
    }

    private ToolExecutionApprovalService CreateOperationService(
        OrizonAgentsDbContext dbContext)
    {
        var service = new ToolExecutionApprovalService(
            _dbContextFactory,
            _currentTenant,
            _executionFactory);
        service._dbContext = dbContext;
        return service;
    }

    private void EnsureCurrentTenant(Guid tenantId)
    {
        if (!_currentTenant.HasTenant ||
            _currentTenant.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                "A Tool não pertence ao tenant atual.");
        }
    }
}
