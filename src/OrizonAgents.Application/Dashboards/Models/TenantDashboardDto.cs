namespace OrizonAgents.Application.Dashboards.Models;

public sealed record TenantDashboardDto(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string TenantStatus,
    string Culture,
    string TimeZone,
    IReadOnlyCollection<DashboardMetricDto> Metrics,
    IReadOnlyCollection<DashboardAgentDto> Agents,
    IReadOnlyCollection<DashboardConfigurationStateDto> ConfigurationStates,
    IReadOnlyCollection<RecentUserDto> RecentUsers,
    IReadOnlyCollection<SetupChecklistItemDto> SetupChecklist);
