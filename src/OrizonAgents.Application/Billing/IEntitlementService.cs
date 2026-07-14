using OrizonAgents.Application.Billing.Models;

namespace OrizonAgents.Application.Billing;

public interface IEntitlementService
{
    Task<EntitlementUsageDto> GetUsageAsync(Guid tenantId, string featureKey, CancellationToken cancellationToken = default);
    Task<bool> IsFeatureEnabledAsync(Guid tenantId, string featureKey, CancellationToken cancellationToken = default);
    Task<bool> HasAvailableCapacityAsync(Guid tenantId, string featureKey, int increment = 1, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EntitlementUsageDto>> GetTenantUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
