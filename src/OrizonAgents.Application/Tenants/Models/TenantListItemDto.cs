namespace OrizonAgents.Application.Tenants.Models;

public sealed record TenantListItemDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    int TotalUsers,
    DateTime CreatedAtUtc);
