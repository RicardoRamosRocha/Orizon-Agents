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

    private readonly OrizonAgentsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private const string OpenEquivalentRequestIndexName =
        "IX_ToolExecutionApprovals_OpenEquivalentRequest";

    private readonly ISensitiveToolExecutionFactory? _executionFactory;

    public ToolExecutionApprovalService(
        OrizonAgentsDbContext dbContext,
        ICurrentTenant currentTenant,
        ISensitiveToolExecutionFactory? executionFactory = null)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _executionFactory = executionFactory;
    }

    public async Task<IReadOnlyList<ToolExecutionApprovalListItemDto>> ListPendingAsync(
        CancellationToken cancellationToken = default)
    {
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
                approval.Expire(utcNow);
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
        EnsureCurrentTenant();

        ToolExecutionApproval? approval =
            await FindApprovalAsync(
                approvalId,
                cancellationToken);

        if (approval is null)
        {
            return false;
        }

        DateTime utcNow = DateTime.UtcNow;

        if (approval.Status != ToolExecutionApprovalStatus.Pending ||
            approval.ExpiresAtUtc <= utcNow)
        {
            if ((approval.Status is ToolExecutionApprovalStatus.Pending or
                 ToolExecutionApprovalStatus.Approved) &&
                approval.ExpiresAtUtc <= utcNow)
            {
                approval.Expire(utcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        approval.Approve(utcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RejectAsync(
        Guid approvalId,
        CancellationToken cancellationToken = default)
    {
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
            approval.Expire(utcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        approval.Reject(utcNow);
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
            .SingleOrDefaultAsync(
                x => x.Id == approvalId,
                cancellationToken);
    }

    private void EnsureCurrentTenant()
    {
        if (!_currentTenant.HasTenant)
        {
            throw new InvalidOperationException(
                "Não há tenant ativo para autorizar a execução da Tool.");
        }
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
