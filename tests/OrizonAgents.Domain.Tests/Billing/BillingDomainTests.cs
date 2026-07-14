using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Domain.Tests.Billing;

public class BillingDomainTests
{
    [Fact]
    public void CreatePlan_WithNegativePrice_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionPlan.Create("Pro", "pro", "", -1, 0));
    }

    [Fact]
    public void PlanCode_NormalizesAndCodeIsImmutable()
    {
        SubscriptionPlan plan = SubscriptionPlan.Create("Pró", "plano pró", "", 10, 100);

        plan.Update("Pro 2", "Novo", 20, 200, "BRL", 0, true, 1);

        Assert.Equal("PLANO_PRO", plan.Code);
    }

    [Fact]
    public void Entitlement_NullLimitMeansUnlimited()
    {
        SubscriptionPlan plan = SubscriptionPlan.Create("Legacy", "legacy", "", 0, 0);

        plan.SetEntitlement(PlanFeatureKeys.Users, true, null);

        Assert.Null(plan.Entitlements.Single().LimitValue);
    }

    [Fact]
    public void Subscription_InvalidTransitionFromCanceledToSuspend_Throws()
    {
        TenantSubscription subscription = TenantSubscription.Start(Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, DateTime.UtcNow, 0);
        subscription.CancelImmediately(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => subscription.Suspend(DateTime.UtcNow));
    }

    [Fact]
    public void Subscription_TrialCanExpire()
    {
        DateTime now = DateTime.UtcNow;
        TenantSubscription subscription = TenantSubscription.Start(Guid.NewGuid(), Guid.NewGuid(), BillingCycle.Monthly, now, 1);

        subscription.ExpireTrial(now.AddDays(2));

        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
    }
}
