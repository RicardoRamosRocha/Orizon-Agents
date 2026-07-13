namespace OrizonAgents.Application.Dashboards.Models;

public sealed record RecentTenantDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    DateTime CreatedAtUtc);
