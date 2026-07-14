namespace OrizonAgents.Application.Billing.Models;

public sealed record PlanEntitlementDto(string FeatureKey, bool IsEnabled, int? LimitValue);

public sealed record PlanListItemDto(
    Guid Id,
    string Name,
    string Code,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    string Currency,
    bool IsPublic,
    bool IsActive,
    bool IsArchived,
    int SortOrder,
    int SubscriptionCount,
    string ConcurrencyStamp);

public sealed record PlanDetailsDto(
    Guid Id,
    string Name,
    string Code,
    string Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    string Currency,
    int TrialDays,
    bool IsPublic,
    bool IsActive,
    bool IsArchived,
    int SortOrder,
    string ConcurrencyStamp,
    IReadOnlyCollection<PlanEntitlementDto> Entitlements,
    int SubscriptionCount);

public sealed record EntitlementUsageDto(
    string FeatureKey,
    bool IsEnabled,
    int? LimitValue,
    int Used,
    int? Available,
    bool IsUnlimited,
    bool IsLimitReached);

public sealed record SubscriptionListItemDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string PlanName,
    string PlanCode,
    string Status,
    string BillingCycle,
    DateTime CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd);

public sealed record SubscriptionDetailsDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    Guid PlanId,
    string PlanName,
    string PlanCode,
    string Status,
    string BillingCycle,
    DateTime StartedAtUtc,
    DateTime? TrialEndsAtUtc,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    bool CancelAtPeriodEnd,
    DateTime? CanceledAtUtc,
    string ConcurrencyStamp,
    IReadOnlyCollection<EntitlementUsageDto> Usage);

public sealed record SubscriptionHistoryDto(
    string Event,
    string Description,
    string? PreviousStatus,
    string? NewStatus,
    DateTime OccurredAtUtc,
    Guid? ActorUserId);

public sealed record TenantBillingDto(
    SubscriptionDetailsDto Subscription,
    IReadOnlyCollection<PlanListItemDto> PublicPlans,
    IReadOnlyCollection<SubscriptionHistoryDto> History);

public sealed record BillingDashboardDto(
    int ActivePlans,
    int TrialingSubscriptions,
    int ActiveSubscriptions,
    int PastDueSubscriptions,
    int SuspendedSubscriptions,
    int ExpiringSoonSubscriptions,
    int TenantsOverUserLimit);
