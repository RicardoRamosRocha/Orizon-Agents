using OrizonAgents.Domain.Common;
using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Domain.Billing;

public sealed class SubscriptionHistory : Entity, ITenantOwnedEntity
{
    private SubscriptionHistory()
    {
        Event = string.Empty;
        Description = string.Empty;
        Tenant = null!;
        Subscription = null!;
    }

    private SubscriptionHistory(
        Guid tenantId,
        Guid tenantSubscriptionId,
        string @event,
        Guid? previousPlanId,
        Guid? newPlanId,
        SubscriptionStatus? previousStatus,
        SubscriptionStatus? newStatus,
        DateTime occurredAtUtc,
        Guid? actorUserId,
        string description)
    {
        TenantId = tenantId;
        TenantSubscriptionId = tenantSubscriptionId;
        Event = @event.Trim();
        PreviousPlanId = previousPlanId;
        NewPlanId = newPlanId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        Description = description.Trim();
    }

    public Guid TenantId { get; private set; }

    public Guid TenantSubscriptionId { get; private set; }

    public string Event { get; private set; }

    public Guid? PreviousPlanId { get; private set; }

    public Guid? NewPlanId { get; private set; }

    public SubscriptionStatus? PreviousStatus { get; private set; }

    public SubscriptionStatus? NewStatus { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string Description { get; private set; }

    public Tenant Tenant { get; private set; }
        = null!;

    public TenantSubscription Subscription { get; private set; }
        = null!;

    public static SubscriptionHistory Create(
        Guid tenantId,
        Guid tenantSubscriptionId,
        string @event,
        Guid? previousPlanId,
        Guid? newPlanId,
        SubscriptionStatus? previousStatus,
        SubscriptionStatus? newStatus,
        DateTime occurredAtUtc,
        Guid? actorUserId,
        string description)
    {
        return new SubscriptionHistory(tenantId, tenantSubscriptionId, @event, previousPlanId, newPlanId, previousStatus, newStatus, occurredAtUtc, actorUserId, description);
    }
}
