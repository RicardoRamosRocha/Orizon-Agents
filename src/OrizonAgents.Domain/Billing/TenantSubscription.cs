using OrizonAgents.Domain.Common;
using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Domain.Billing;

public sealed class TenantSubscription : AuditableEntity, ITenantOwnedEntity
{
    private TenantSubscription()
    {
        ConcurrencyStamp = string.Empty;
        Tenant = null!;
        Plan = null!;
    }

    private TenantSubscription(
        Guid tenantId,
        Guid subscriptionPlanId,
        SubscriptionStatus status,
        BillingCycle billingCycle,
        DateTime startedAtUtc,
        DateTime? trialEndsAtUtc,
        DateTime currentPeriodStartUtc,
        DateTime currentPeriodEndUtc)
    {
        EnsureUtc(startedAtUtc);
        if (trialEndsAtUtc.HasValue) EnsureUtc(trialEndsAtUtc.Value);
        EnsureUtc(currentPeriodStartUtc);
        EnsureUtc(currentPeriodEndUtc);

        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        SubscriptionPlanId = subscriptionPlanId != Guid.Empty ? subscriptionPlanId : throw new ArgumentException("Plano é obrigatório.", nameof(subscriptionPlanId));
        Status = status;
        BillingCycle = billingCycle;
        StartedAtUtc = startedAtUtc;
        TrialEndsAtUtc = trialEndsAtUtc;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc > currentPeriodStartUtc
            ? currentPeriodEndUtc
            : throw new ArgumentException("Período atual inválido.", nameof(currentPeriodEndUtc));
        ConcurrencyStamp = NewConcurrencyStamp();
    }

    public Guid TenantId { get; private set; }

    public Guid SubscriptionPlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public BillingCycle BillingCycle { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? TrialEndsAtUtc { get; private set; }

    public DateTime CurrentPeriodStartUtc { get; private set; }

    public DateTime CurrentPeriodEndUtc { get; private set; }

    public bool CancelAtPeriodEnd { get; private set; }

    public DateTime? CanceledAtUtc { get; private set; }

    public string ConcurrencyStamp { get; private set; }

    public Tenant Tenant { get; private set; }
        = null!;

    public SubscriptionPlan Plan { get; private set; }
        = null!;

    public static TenantSubscription Start(
        Guid tenantId,
        Guid subscriptionPlanId,
        BillingCycle billingCycle,
        DateTime utcNow,
        int trialDays)
    {
        DateTime periodEnd = billingCycle == BillingCycle.Yearly ? utcNow.AddYears(1) : utcNow.AddMonths(1);
        return new TenantSubscription(
            tenantId,
            subscriptionPlanId,
            trialDays > 0 ? SubscriptionStatus.Trialing : SubscriptionStatus.Active,
            billingCycle,
            utcNow,
            trialDays > 0 ? utcNow.AddDays(trialDays) : null,
            utcNow,
            periodEnd);
    }

    public void ChangePlan(Guid newPlanId, BillingCycle billingCycle, DateTime utcNow)
    {
        EnsureMutable();
        EnsureUtc(utcNow);
        SubscriptionPlanId = newPlanId;
        BillingCycle = billingCycle;
        CurrentPeriodStartUtc = utcNow;
        CurrentPeriodEndUtc = billingCycle == BillingCycle.Yearly ? utcNow.AddYears(1) : utcNow.AddMonths(1);
        CancelAtPeriodEnd = false;
        TouchConcurrency();
    }

    public void Activate(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
        {
            throw new InvalidOperationException("Assinatura encerrada não pode ser ativada diretamente.");
        }

        Status = SubscriptionStatus.Active;
        TrialEndsAtUtc = null;
        CancelAtPeriodEnd = false;
        TouchConcurrency();
    }

    public void ScheduleCancellation(DateTime utcNow)
    {
        EnsureMutable();
        EnsureUtc(utcNow);
        CancelAtPeriodEnd = true;
        CanceledAtUtc = utcNow;
        TouchConcurrency();
    }

    public void CancelImmediately(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status == SubscriptionStatus.Canceled)
        {
            return;
        }

        Status = SubscriptionStatus.Canceled;
        CancelAtPeriodEnd = false;
        CanceledAtUtc = utcNow;
        CurrentPeriodEndUtc = utcNow;
        TouchConcurrency();
    }

    public void Reactivate(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status is not (SubscriptionStatus.Canceled or SubscriptionStatus.Suspended or SubscriptionStatus.Expired))
        {
            throw new InvalidOperationException("Somente assinaturas canceladas, suspensas ou expiradas podem ser reativadas.");
        }

        Status = SubscriptionStatus.Active;
        CanceledAtUtc = null;
        CancelAtPeriodEnd = false;
        CurrentPeriodStartUtc = utcNow;
        CurrentPeriodEndUtc = BillingCycle == BillingCycle.Yearly ? utcNow.AddYears(1) : utcNow.AddMonths(1);
        TouchConcurrency();
    }

    public void Suspend(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
        {
            throw new InvalidOperationException("Assinatura encerrada não pode ser suspensa.");
        }

        Status = SubscriptionStatus.Suspended;
        TouchConcurrency();
    }

    public void ExpireTrial(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status == SubscriptionStatus.Trialing && TrialEndsAtUtc <= utcNow)
        {
            Status = SubscriptionStatus.Expired;
            TouchConcurrency();
        }
    }

    public void RenewPeriod(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        if (Status != SubscriptionStatus.Active || CurrentPeriodEndUtc > utcNow)
        {
            return;
        }

        if (CancelAtPeriodEnd)
        {
            Status = SubscriptionStatus.Canceled;
            CanceledAtUtc ??= utcNow;
        }
        else
        {
            CurrentPeriodStartUtc = CurrentPeriodEndUtc;
            CurrentPeriodEndUtc = BillingCycle == BillingCycle.Yearly
                ? CurrentPeriodEndUtc.AddYears(1)
                : CurrentPeriodEndUtc.AddMonths(1);
        }

        TouchConcurrency();
    }

    public void EnsureConcurrencyStamp(string concurrencyStamp)
    {
        if (!string.Equals(ConcurrencyStamp, concurrencyStamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A assinatura foi alterada por outro usuário. Recarregue a página e tente novamente.");
        }
    }

    private void EnsureMutable()
    {
        if (Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
        {
            throw new InvalidOperationException("Assinatura encerrada não aceita esta operação.");
        }
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Datas devem estar em UTC.", nameof(dateTime));
        }
    }

    private void TouchConcurrency()
    {
        ConcurrencyStamp = NewConcurrencyStamp();
    }

    private static string NewConcurrencyStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
