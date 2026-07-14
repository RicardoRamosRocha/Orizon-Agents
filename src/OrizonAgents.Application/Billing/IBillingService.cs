using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Billing.Requests;
using OrizonAgents.Application.Common.Paging;
using OrizonAgents.Application.Common.Results;

namespace OrizonAgents.Application.Billing;

public interface IBillingService
{
    Task<PagedResult<PlanListItemDto>> ListPlansAsync(PlanListRequest request, CancellationToken cancellationToken = default);
    Task<PlanDetailsDto?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PlanListItemDto>> GetPublicPlansAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> CreatePlanAsync(PlanUpsertRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdatePlanAsync(Guid planId, PlanUpsertRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> SetPlanArchiveStateAsync(Guid planId, bool archived, string concurrencyStamp, CancellationToken cancellationToken = default);
    Task<OperationResult> SetPlanActiveStateAsync(Guid planId, bool isActive, string concurrencyStamp, CancellationToken cancellationToken = default);
    Task<PagedResult<SubscriptionListItemDto>> ListSubscriptionsAsync(SubscriptionListRequest request, CancellationToken cancellationToken = default);
    Task<SubscriptionDetailsDto?> GetSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<TenantBillingDto?> GetTenantBillingAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<OperationResult<Guid>> AssignPlanAsync(AssignSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ChangePlanAsync(ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ScheduleCancellationAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> CancelImmediatelyAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> ReactivateAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> SuspendAsync(SubscriptionActionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SubscriptionHistoryDto>> GetHistoryAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<BillingDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
