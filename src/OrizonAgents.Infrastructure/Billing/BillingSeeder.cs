using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Billing;

public static class BillingSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrizonAgentsDbContext>();

        SubscriptionPlan? legacy = await dbContext.SubscriptionPlans
            .Include(plan => plan.Entitlements)
            .SingleOrDefaultAsync(plan => plan.Code == PlanCode.Legacy, cancellationToken);

        if (legacy is null)
        {
            legacy = SubscriptionPlan.Create(
                "Legacy",
                PlanCode.Legacy,
                "Plano interno para tenants existentes.",
                0,
                0,
                "BRL",
                trialDays: 0,
                isPublic: false,
                sortOrder: 0);

            foreach (string featureKey in PlanFeatureKeys.All)
            {
                legacy.SetEntitlement(featureKey, isEnabled: true, limitValue: null);
            }

            dbContext.SubscriptionPlans.Add(legacy);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        Guid[] tenantIds = await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => !dbContext.TenantSubscriptions.Any(subscription => subscription.TenantId == tenant.Id))
            .Select(tenant => tenant.Id)
            .ToArrayAsync(cancellationToken);

        foreach (Guid tenantId in tenantIds)
        {
            var subscription = TenantSubscription.Start(
                tenantId,
                legacy.Id,
                BillingCycle.Monthly,
                DateTime.UtcNow,
                trialDays: 0);
            dbContext.TenantSubscriptions.Add(subscription);
            dbContext.SubscriptionHistories.Add(SubscriptionHistory.Create(
                tenantId,
                subscription.Id,
                "legacy_assigned",
                null,
                legacy.Id,
                null,
                subscription.Status,
                DateTime.UtcNow,
                null,
                "Plano Legacy associado automaticamente ao tenant existente."));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
