namespace OrizonAgents.Application.Dashboards.Models;

public sealed record PlatformDashboardDto(
    IReadOnlyCollection<DashboardMetricDto> Metrics,
    IReadOnlyCollection<RecentTenantDto> RecentTenants,
    IReadOnlyCollection<RecentUserDto> RecentUsers,
    IReadOnlyCollection<SetupChecklistItemDto> TechnicalStatus);
