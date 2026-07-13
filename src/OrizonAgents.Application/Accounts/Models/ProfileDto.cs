namespace OrizonAgents.Application.Accounts.Models;

public sealed record ProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? TenantName,
    string? TenantSlug);
