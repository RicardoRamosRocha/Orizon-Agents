namespace OrizonAgents.Application.Tenants.Requests;

public sealed record UpdateTenantRequest(
    Guid TenantId,
    string Name,
    string Slug,
    string Culture,
    string TimeZone,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string ConcurrencyStamp);
