namespace OrizonAgents.Application.Tenants.Requests;

public sealed record UpdateOwnTenantSettingsRequest(
    Guid TenantId,
    string Name,
    string Culture,
    string TimeZone,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string ConcurrencyStamp);
