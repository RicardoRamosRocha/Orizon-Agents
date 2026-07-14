using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Billing.Requests;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Billing;

public sealed class BillingService : IBillingService
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IEntitlementService _entitlementService;

    public BillingService(OrizonAgentsDbContext dbContext, IEntitlementService entitlementService)
    {
        _dbContext = dbContext;
        _entitlementService = entitlementService;
    }

    public async Task<PagedResult<PlanListItemDto>> ListPlansAsync(PlanListRequest request, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, request.PageNumber);
        int size = Math.Clamp(request.PageSize, 5, 50);
        IQueryable<SubscriptionPlan> query = _dbContext.SubscriptionPlans.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim().ToUpperInvariant();
            query = query.Where(plan => plan.Name.ToUpper().Contains(search) || plan.Code.Contains(search));
        }

        if (request.IsActive.HasValue) query = query.Where(plan => plan.IsActive == request.IsActive.Value);
        if (request.IsPublic.HasValue) query = query.Where(plan => plan.IsPublic == request.IsPublic.Value);

        int total = await query.CountAsync(cancellationToken);
        PlanListItemDto[] items = await query
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(plan => new PlanListItemDto(
                plan.Id,
                plan.Name,
                plan.Code,
                plan.MonthlyPrice,
                plan.YearlyPrice,
                plan.Currency,
                plan.IsPublic,
                plan.IsActive,
                plan.IsArchived,
                plan.SortOrder,
                _dbContext.TenantSubscriptions.Count(subscription => subscription.SubscriptionPlanId == plan.Id),
                plan.ConcurrencyStamp))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<PlanListItemDto>(items, page, size, total);
    }

    public async Task<PlanDetailsDto?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SubscriptionPlans.AsNoTracking()
            .Where(plan => plan.Id == planId)
            .Select(plan => new PlanDetailsDto(
                plan.Id,
                plan.Name,
                plan.Code,
                plan.Description,
                plan.MonthlyPrice,
                plan.YearlyPrice,
                plan.Currency,
                plan.TrialDays,
                plan.IsPublic,
                plan.IsActive,
                plan.IsArchived,
                plan.SortOrder,
                plan.ConcurrencyStamp,
                plan.Entitlements.OrderBy(entitlement => entitlement.FeatureKey).Select(entitlement => new PlanEntitlementDto(entitlement.FeatureKey, entitlement.IsEnabled, entitlement.LimitValue)).ToArray(),
                _dbContext.TenantSubscriptions.Count(subscription => subscription.SubscriptionPlanId == plan.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PlanListItemDto>> GetPublicPlansAsync(CancellationToken cancellationToken = default)
    {
        return (await ListPlansAsync(new PlanListRequest(null, true, true, 1, 50), cancellationToken)).Items;
    }

    public async Task<OperationResult<Guid>> CreatePlanAsync(PlanUpsertRequest request, CancellationToken cancellationToken = default)
    {
        string code = PlanCode.Create(request.Code);
        if (await _dbContext.SubscriptionPlans.AnyAsync(plan => plan.Code == code, cancellationToken))
        {
            return OperationResult<Guid>.Failure("Já existe um plano com este código.");
        }

        try
        {
            SubscriptionPlan plan = SubscriptionPlan.Create(request.Name, code, request.Description, request.MonthlyPrice, request.YearlyPrice, request.Currency, request.TrialDays, request.IsPublic, request.SortOrder);
            foreach (PlanEntitlementRequest entitlement in NormalizeEntitlements(request.Entitlements))
            {
                plan.SetEntitlement(entitlement.FeatureKey, entitlement.IsEnabled, entitlement.LimitValue);
            }

            _dbContext.SubscriptionPlans.Add(plan);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult<Guid>.Success(plan.Id);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return OperationResult<Guid>.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> UpdatePlanAsync(Guid planId, PlanUpsertRequest request, CancellationToken cancellationToken = default)
    {
        SubscriptionPlan? plan = await _dbContext.SubscriptionPlans.Include(candidate => candidate.Entitlements).SingleOrDefaultAsync(candidate => candidate.Id == planId, cancellationToken);
        if (plan is null) return OperationResult.Failure("Plano não encontrado.");

        try
        {
            plan.EnsureConcurrencyStamp(request.ConcurrencyStamp ?? string.Empty);
            plan.Update(request.Name, request.Description, request.MonthlyPrice, request.YearlyPrice, request.Currency, request.TrialDays, request.IsPublic, request.SortOrder);
            foreach (PlanEntitlementRequest entitlement in NormalizeEntitlements(request.Entitlements))
            {
                plan.SetEntitlement(entitlement.FeatureKey, entitlement.IsEnabled, entitlement.LimitValue);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> SetPlanArchiveStateAsync(Guid planId, bool archived, string concurrencyStamp, CancellationToken cancellationToken = default)
    {
        SubscriptionPlan? plan = await _dbContext.SubscriptionPlans.SingleOrDefaultAsync(candidate => candidate.Id == planId, cancellationToken);
        if (plan is null) return OperationResult.Failure("Plano não encontrado.");
        try
        {
            plan.EnsureConcurrencyStamp(concurrencyStamp);
            if (archived) plan.Archive();
            else return OperationResult.Failure("Plano arquivado não pode ser restaurado automaticamente.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> SetPlanActiveStateAsync(Guid planId, bool isActive, string concurrencyStamp, CancellationToken cancellationToken = default)
    {
        SubscriptionPlan? plan = await _dbContext.SubscriptionPlans.SingleOrDefaultAsync(candidate => candidate.Id == planId, cancellationToken);
        if (plan is null) return OperationResult.Failure("Plano não encontrado.");
        try
        {
            plan.EnsureConcurrencyStamp(concurrencyStamp);
            if (isActive) plan.Activate(); else plan.Deactivate();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<PagedResult<SubscriptionListItemDto>> ListSubscriptionsAsync(SubscriptionListRequest request, CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, request.PageNumber);
        int size = Math.Clamp(request.PageSize, 5, 50);
        IQueryable<TenantSubscription> query = _dbContext.TenantSubscriptions.AsNoTracking().Include(subscription => subscription.Tenant).Include(subscription => subscription.Plan);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(subscription => subscription.Tenant.Name.ToLower().Contains(search) || subscription.Tenant.Slug.ToLower().Contains(search));
        }
        if (request.PlanId.HasValue) query = query.Where(subscription => subscription.SubscriptionPlanId == request.PlanId.Value);
        if (Enum.TryParse(request.Status, true, out SubscriptionStatus status)) query = query.Where(subscription => subscription.Status == status);

        int total = await query.CountAsync(cancellationToken);
        SubscriptionListItemDto[] items = await query.OrderBy(subscription => subscription.Tenant.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(subscription => new SubscriptionListItemDto(subscription.Id, subscription.TenantId, subscription.Tenant.Name, subscription.Plan.Name, subscription.Plan.Code, subscription.Status.ToString(), subscription.BillingCycle.ToString(), subscription.CurrentPeriodEndUtc, subscription.CancelAtPeriodEnd))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<SubscriptionListItemDto>(items, page, size, total);
    }

    public async Task<SubscriptionDetailsDto?> GetSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var dto = await _dbContext.TenantSubscriptions.AsNoTracking()
            .Where(subscription => subscription.Id == subscriptionId)
            .Select(subscription => new
            {
                subscription.Id,
                subscription.TenantId,
                TenantName = subscription.Tenant.Name,
                PlanId = subscription.SubscriptionPlanId,
                PlanName = subscription.Plan.Name,
                PlanCode = subscription.Plan.Code,
                Status = subscription.Status.ToString(),
                BillingCycle = subscription.BillingCycle.ToString(),
                subscription.StartedAtUtc,
                subscription.TrialEndsAtUtc,
                subscription.CurrentPeriodStartUtc,
                subscription.CurrentPeriodEndUtc,
                subscription.CancelAtPeriodEnd,
                subscription.CanceledAtUtc,
                subscription.ConcurrencyStamp
            }).SingleOrDefaultAsync(cancellationToken);
        if (dto is null) return null;
        return new SubscriptionDetailsDto(dto.Id, dto.TenantId, dto.TenantName, dto.PlanId, dto.PlanName, dto.PlanCode, dto.Status, dto.BillingCycle, dto.StartedAtUtc, dto.TrialEndsAtUtc, dto.CurrentPeriodStartUtc, dto.CurrentPeriodEndUtc, dto.CancelAtPeriodEnd, dto.CanceledAtUtc, dto.ConcurrencyStamp, await _entitlementService.GetTenantUsageAsync(dto.TenantId, cancellationToken));
    }

    public async Task<TenantBillingDto?> GetTenantBillingAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        Guid? subscriptionId = await _dbContext.TenantSubscriptions.AsNoTracking().Where(subscription => subscription.TenantId == tenantId).Select(subscription => (Guid?)subscription.Id).SingleOrDefaultAsync(cancellationToken);
        if (!subscriptionId.HasValue) return null;
        SubscriptionDetailsDto? subscription = await GetSubscriptionAsync(subscriptionId.Value, cancellationToken);
        if (subscription is null) return null;
        return new TenantBillingDto(subscription, await GetPublicPlansAsync(cancellationToken), await GetHistoryAsync(subscription.Id, cancellationToken));
    }

    public async Task<OperationResult<Guid>> AssignPlanAsync(AssignSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        SubscriptionPlan? plan = await _dbContext.SubscriptionPlans.SingleOrDefaultAsync(candidate => candidate.Id == request.PlanId, cancellationToken);
        if (plan is null || !plan.IsActive || plan.IsArchived) return OperationResult<Guid>.Failure("Plano indisponível para nova assinatura.");
        if (await _dbContext.TenantSubscriptions.AnyAsync(subscription => subscription.TenantId == request.TenantId, cancellationToken)) return OperationResult<Guid>.Failure("Tenant já possui assinatura atual.");

        TenantSubscription subscription = TenantSubscription.Start(request.TenantId, request.PlanId, request.BillingCycle, DateTime.UtcNow, request.StartTrial ? plan.TrialDays : 0);
        _dbContext.TenantSubscriptions.Add(subscription);
        AddHistory(subscription, "assigned", null, subscription.Status, null, request.PlanId, request.ActorUserId, "Plano atribuído ao tenant.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<Guid>.Success(subscription.Id);
    }

    public Task<OperationResult> ChangePlanAsync(ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
        => MutateSubscriptionAsync(request.SubscriptionId, request.ConcurrencyStamp, request.ActorUserId, "plan_changed", "Plano da assinatura alterado.", async subscription =>
        {
            SubscriptionPlan? plan = await _dbContext.SubscriptionPlans.SingleOrDefaultAsync(candidate => candidate.Id == request.NewPlanId, cancellationToken);
            if (plan is null || !plan.IsActive || plan.IsArchived) throw new InvalidOperationException("Plano indisponível.");
            Guid previousPlanId = subscription.SubscriptionPlanId;
            subscription.ChangePlan(request.NewPlanId, request.BillingCycle, DateTime.UtcNow);
            return previousPlanId;
        }, cancellationToken);

    public Task<OperationResult> ScheduleCancellationAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default)
        => MutateSubscriptionAsync(request.SubscriptionId, request.ConcurrencyStamp, request.ActorUserId, "cancel_scheduled", "Cancelamento agendado para o fim do período.", subscription => { subscription.ScheduleCancellation(DateTime.UtcNow); return Task.FromResult<Guid?>(subscription.SubscriptionPlanId); }, cancellationToken);

    public Task<OperationResult> CancelImmediatelyAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default)
        => MutateSubscriptionAsync(request.SubscriptionId, request.ConcurrencyStamp, request.ActorUserId, "canceled", "Assinatura cancelada imediatamente.", subscription => { subscription.CancelImmediately(DateTime.UtcNow); return Task.FromResult<Guid?>(subscription.SubscriptionPlanId); }, cancellationToken);

    public Task<OperationResult> ReactivateAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default)
        => MutateSubscriptionAsync(request.SubscriptionId, request.ConcurrencyStamp, request.ActorUserId, "reactivated", "Assinatura reativada.", subscription => { subscription.Reactivate(DateTime.UtcNow); return Task.FromResult<Guid?>(subscription.SubscriptionPlanId); }, cancellationToken);

    public Task<OperationResult> SuspendAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default)
        => MutateSubscriptionAsync(request.SubscriptionId, request.ConcurrencyStamp, request.ActorUserId, "suspended", "Assinatura suspensa.", subscription => { subscription.Suspend(DateTime.UtcNow); return Task.FromResult<Guid?>(subscription.SubscriptionPlanId); }, cancellationToken);

    public async Task<IReadOnlyCollection<SubscriptionHistoryDto>> GetHistoryAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SubscriptionHistories.AsNoTracking()
            .Where(history => history.TenantSubscriptionId == subscriptionId)
            .OrderByDescending(history => history.OccurredAtUtc)
            .Select(history => new SubscriptionHistoryDto(history.Event, history.Description, history.PreviousStatus == null ? null : history.PreviousStatus.ToString(), history.NewStatus == null ? null : history.NewStatus.ToString(), history.OccurredAtUtc, history.ActorUserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<BillingDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        int activePlans = await _dbContext.SubscriptionPlans.AsNoTracking().CountAsync(plan => plan.IsActive && !plan.IsArchived, cancellationToken);
        int trialing = await _dbContext.TenantSubscriptions.AsNoTracking().CountAsync(subscription => subscription.Status == SubscriptionStatus.Trialing, cancellationToken);
        int active = await _dbContext.TenantSubscriptions.AsNoTracking().CountAsync(subscription => subscription.Status == SubscriptionStatus.Active, cancellationToken);
        int pastDue = await _dbContext.TenantSubscriptions.AsNoTracking().CountAsync(subscription => subscription.Status == SubscriptionStatus.PastDue, cancellationToken);
        int suspended = await _dbContext.TenantSubscriptions.AsNoTracking().CountAsync(subscription => subscription.Status == SubscriptionStatus.Suspended, cancellationToken);
        DateTime soon = DateTime.UtcNow.AddDays(7);
        int expiring = await _dbContext.TenantSubscriptions.AsNoTracking().CountAsync(subscription => subscription.CurrentPeriodEndUtc <= soon && subscription.Status != SubscriptionStatus.Canceled, cancellationToken);
        int overUsers = 0;
        Guid[] tenantIds = await _dbContext.TenantSubscriptions.AsNoTracking().Select(subscription => subscription.TenantId).ToArrayAsync(cancellationToken);
        foreach (Guid tenantId in tenantIds)
        {
            EntitlementUsageDto usage = await _entitlementService.GetUsageAsync(tenantId, PlanFeatureKeys.Users, cancellationToken);
            if (usage.LimitValue.HasValue && usage.Used > usage.LimitValue.Value) overUsers++;
        }
        return new BillingDashboardDto(activePlans, trialing, active, pastDue, suspended, expiring, overUsers);
    }

    private async Task<OperationResult> MutateSubscriptionAsync(Guid id, string stamp, Guid? actor, string @event, string description, Func<TenantSubscription, Task<Guid?>> operation, CancellationToken cancellationToken)
    {
        TenantSubscription? subscription = await _dbContext.TenantSubscriptions.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (subscription is null) return OperationResult.Failure("Assinatura não encontrada.");
        try
        {
            subscription.EnsureConcurrencyStamp(stamp);
            SubscriptionStatus previousStatus = subscription.Status;
            Guid previousPlanId = subscription.SubscriptionPlanId;
            Guid? operationPreviousPlan = await operation(subscription);
            AddHistory(subscription, @event, previousStatus, subscription.Status, operationPreviousPlan ?? previousPlanId, subscription.SubscriptionPlanId, actor, description);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    private void AddHistory(TenantSubscription subscription, string @event, SubscriptionStatus? previousStatus, SubscriptionStatus? newStatus, Guid? previousPlanId, Guid? newPlanId, Guid? actor, string description)
    {
        _dbContext.SubscriptionHistories.Add(SubscriptionHistory.Create(subscription.TenantId, subscription.Id, @event, previousPlanId, newPlanId, previousStatus, newStatus, DateTime.UtcNow, actor, description));
    }

    private static IReadOnlyCollection<PlanEntitlementRequest> NormalizeEntitlements(IReadOnlyCollection<PlanEntitlementRequest> entitlements)
    {
        var byKey = entitlements.ToDictionary(item => item.FeatureKey.Trim().ToLowerInvariant(), item => item);
        return PlanFeatureKeys.All
            .Select(key => byKey.TryGetValue(key, out PlanEntitlementRequest? item) ? item with { FeatureKey = key } : new PlanEntitlementRequest(key, false, 0))
            .ToArray();
    }
}
