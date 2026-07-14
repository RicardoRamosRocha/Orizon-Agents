using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.Billing;

public sealed class PlanEntitlement : Entity
{
    private PlanEntitlement()
    {
        FeatureKey = string.Empty;
        Plan = null!;
    }

    private PlanEntitlement(Guid subscriptionPlanId, string featureKey, bool isEnabled, int? limitValue)
    {
        SubscriptionPlanId = subscriptionPlanId;
        FeatureKey = featureKey;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
    }

    public Guid SubscriptionPlanId { get; private set; }

    public string FeatureKey { get; private set; }

    public bool IsEnabled { get; private set; }

    public int? LimitValue { get; private set; }

    public SubscriptionPlan Plan { get; private set; }
        = null!;

    public static PlanEntitlement Create(Guid subscriptionPlanId, string featureKey, bool isEnabled, int? limitValue)
    {
        return new PlanEntitlement(subscriptionPlanId, featureKey, isEnabled, limitValue);
    }

    public void Update(bool isEnabled, int? limitValue)
    {
        if (limitValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitValue), "Limite não pode ser negativo.");
        }

        IsEnabled = isEnabled;
        LimitValue = limitValue;
    }
}
