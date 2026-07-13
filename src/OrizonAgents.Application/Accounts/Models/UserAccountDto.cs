namespace OrizonAgents.Application.Accounts.Models;

public sealed record UserAccountDto(
    Guid Id,
    Guid? TenantId,
    string FullName,
    string Email,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyCollection<string> Roles);
