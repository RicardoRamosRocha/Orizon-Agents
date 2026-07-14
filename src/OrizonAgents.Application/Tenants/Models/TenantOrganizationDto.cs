namespace OrizonAgents.Application.Tenants.Models;

public sealed record TenantOrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string Culture,
    string TimeZone,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string ConcurrencyStamp);
