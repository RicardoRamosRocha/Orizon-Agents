namespace OrizonAgents.Application.Tenants.Requests;

public sealed record CreateTenantRequest(
    string Name,
    string Slug,
    string Culture,
    string TimeZone,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword,
    string AdminConfirmPassword);
