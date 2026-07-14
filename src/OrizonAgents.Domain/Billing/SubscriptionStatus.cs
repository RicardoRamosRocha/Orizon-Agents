namespace OrizonAgents.Domain.Billing;

public enum SubscriptionStatus
{
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Suspended = 4,
    Canceled = 5,
    Expired = 6
}
