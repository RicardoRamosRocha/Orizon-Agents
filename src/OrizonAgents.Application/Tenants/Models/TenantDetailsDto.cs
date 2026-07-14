using OrizonAgents.Application.Dashboards.Models;

namespace OrizonAgents.Application.Tenants.Models;

public sealed record TenantDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string Culture,
    string TimeZone,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? SuspensionReason,
    DateTime? SuspendedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string ConcurrencyStamp,
    int TotalUsers,
    int ActiveUsers,
    int AdminUsers,
    IReadOnlyCollection<RecentUserDto> RecentUsers);
