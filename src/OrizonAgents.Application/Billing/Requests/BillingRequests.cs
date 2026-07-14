using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Application.Billing.Requests;

public sealed record PlanListRequest(string? Search, bool? IsActive, bool? IsPublic, int PageNumber = 1, int PageSize = 10);

public sealed record PlanEntitlementRequest(string FeatureKey, bool IsEnabled, int? LimitValue);

public sealed record PlanUpsertRequest(
    string Name,
    string Code,
    string Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    string Currency,
    int TrialDays,
    bool IsPublic,
    int SortOrder,
    string? ConcurrencyStamp,
    IReadOnlyCollection<PlanEntitlementRequest> Entitlements);

public sealed record SubscriptionListRequest(string? Search, Guid? PlanId, string? Status, int PageNumber = 1, int PageSize = 10);

public sealed record AssignSubscriptionRequest(
    Guid TenantId,
    Guid PlanId,
    BillingCycle BillingCycle,
    bool StartTrial,
    Guid? ActorUserId);

public sealed record ChangeSubscriptionPlanRequest(
    Guid SubscriptionId,
    Guid NewPlanId,
    BillingCycle BillingCycle,
    string ConcurrencyStamp,
    Guid? ActorUserId);

public sealed record SubscriptionActionRequest(
    Guid SubscriptionId,
    string ConcurrencyStamp,
    Guid? ActorUserId);
