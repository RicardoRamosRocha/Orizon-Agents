using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Billing;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Billing;

public sealed class BillingCycleProcessor : IBillingCycleProcessor
{
    private const int BatchSize = 50;
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly ILogger<BillingCycleProcessor> _logger;

    public BillingCycleProcessor(OrizonAgentsDbContext dbContext, ILogger<BillingCycleProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> ProcessAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        int processed = 0;
        Guid[] ids = await _dbContext.TenantSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                (subscription.Status == SubscriptionStatus.Trialing && subscription.TrialEndsAtUtc <= utcNow) ||
                (subscription.CurrentPeriodEndUtc <= utcNow && (subscription.Status == SubscriptionStatus.Active || subscription.CancelAtPeriodEnd)))
            .OrderBy(subscription => subscription.CurrentPeriodEndUtc)
            .Take(BatchSize)
            .Select(subscription => subscription.Id)
            .ToArrayAsync(cancellationToken);

        foreach (Guid id in ids)
        {
            try
            {
                TenantSubscription? subscription = await _dbContext.TenantSubscriptions.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (subscription is null)
                {
                    continue;
                }

                SubscriptionStatus previousStatus = subscription.Status;
                if (subscription.Status == SubscriptionStatus.Trialing)
                {
                    subscription.ExpireTrial(utcNow);
                }
                else
                {
                    subscription.RenewPeriod(utcNow);
                }

                if (previousStatus != subscription.Status)
                {
                    AddHistory(subscription, "cycle_processed", previousStatus, subscription.Status, "Ciclo de billing processado automaticamente.", utcNow);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falha ao processar assinatura {SubscriptionId}", id);
            }
        }

        return processed;
    }

    private void AddHistory(TenantSubscription subscription, string @event, SubscriptionStatus previousStatus, SubscriptionStatus newStatus, string description, DateTime utcNow)
    {
        bool exists = _dbContext.SubscriptionHistories.Any(history =>
            history.TenantSubscriptionId == subscription.Id &&
            history.Event == @event &&
            history.NewStatus == newStatus);
        if (exists)
        {
            return;
        }

        _dbContext.SubscriptionHistories.Add(SubscriptionHistory.Create(
            subscription.TenantId,
            subscription.Id,
            @event,
            subscription.SubscriptionPlanId,
            subscription.SubscriptionPlanId,
            previousStatus,
            newStatus,
            utcNow,
            null,
            description));
    }
}
