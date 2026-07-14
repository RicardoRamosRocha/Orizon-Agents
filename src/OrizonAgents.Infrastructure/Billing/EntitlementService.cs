using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Billing;

public sealed class EntitlementService : IEntitlementService
{
    private readonly OrizonAgentsDbContext _dbContext;

    public EntitlementService(OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EntitlementUsageDto> GetUsageAsync(Guid tenantId, string featureKey, CancellationToken cancellationToken = default)
    {
        string key = featureKey.Trim().ToLowerInvariant();
        var entitlement = await (
            from subscription in _dbContext.TenantSubscriptions.AsNoTracking()
            join plan in _dbContext.SubscriptionPlans.AsNoTracking() on subscription.SubscriptionPlanId equals plan.Id
            join candidate in _dbContext.PlanEntitlements.AsNoTracking() on plan.Id equals candidate.SubscriptionPlanId
            where subscription.TenantId == tenantId && candidate.FeatureKey == key
            select new { candidate.IsEnabled, candidate.LimitValue })
            .SingleOrDefaultAsync(cancellationToken);

        int used = await GetUsageCountAsync(tenantId, key, cancellationToken);
        if (entitlement is null)
        {
            return new EntitlementUsageDto(key, true, null, used, null, true, false);
        }

        bool enabled = entitlement?.IsEnabled == true;
        int? limit = entitlement?.LimitValue;
        bool isUnlimited = enabled && limit is null;
        int? available = !enabled ? 0 : limit.HasValue ? Math.Max(0, limit.Value - used) : null;

        return new EntitlementUsageDto(
            key,
            enabled,
            limit,
            used,
            available,
            isUnlimited,
            enabled && limit.HasValue && used >= limit.Value);
    }

    public async Task<bool> IsFeatureEnabledAsync(Guid tenantId, string featureKey, CancellationToken cancellationToken = default)
    {
        return (await GetUsageAsync(tenantId, featureKey, cancellationToken)).IsEnabled;
    }

    public async Task<bool> HasAvailableCapacityAsync(Guid tenantId, string featureKey, int increment = 1, CancellationToken cancellationToken = default)
    {
        EntitlementUsageDto usage = await GetUsageAsync(tenantId, featureKey, cancellationToken);
        return usage.IsEnabled && (usage.IsUnlimited || (usage.Available ?? 0) >= increment);
    }

    public async Task<IReadOnlyCollection<EntitlementUsageDto>> GetTenantUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = new List<EntitlementUsageDto>();
        foreach (string key in PlanFeatureKeys.All)
        {
            result.Add(await GetUsageAsync(tenantId, key, cancellationToken));
        }

        return result;
    }

    private async Task<int> GetUsageCountAsync(Guid tenantId, string featureKey, CancellationToken cancellationToken)
    {
        if (featureKey == PlanFeatureKeys.Users)
        {
            return await _dbContext.Users.AsNoTracking().CountAsync(user => user.TenantId == tenantId && user.IsActive, cancellationToken);
        }

        if (featureKey == PlanFeatureKeys.WhatsAppNumbers)
        {
            return await _dbContext.WhatsAppConnections.AsNoTracking().CountAsync(
                connection => connection.TenantId == tenantId && connection.Status != OrizonAgents.Domain.WhatsApp.WhatsAppConnectionStatus.Disconnected,
                cancellationToken);
        }

        if (featureKey == PlanFeatureKeys.MonthlyMessages)
        {
            DateTime utcNow = DateTime.UtcNow;
            return await _dbContext.WhatsAppMonthlyUsage.AsNoTracking()
                .Where(usage => usage.TenantId == tenantId && usage.Year == utcNow.Year && usage.Month == utcNow.Month)
                .Select(usage => usage.OutgoingAcceptedCount)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return 0;
    }
}
