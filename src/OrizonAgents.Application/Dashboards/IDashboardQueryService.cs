using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Dashboards.Models;

namespace OrizonAgents.Application.Dashboards;

public interface IDashboardQueryService
{
    Task<OperationResult<TenantDashboardDto>> GetTenantDashboardAsync(
        Guid tenantId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<PlatformDashboardDto> GetPlatformDashboardAsync(CancellationToken cancellationToken = default);
}
