namespace OrizonAgents.Application.Dashboards.Models;

public sealed record RecentUserDto(
    Guid Id,
    string FullName,
    string Email,
    string? TenantName,
    bool IsActive,
    DateTime CreatedAtUtc);
