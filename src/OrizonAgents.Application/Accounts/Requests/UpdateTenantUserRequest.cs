namespace OrizonAgents.Application.Accounts.Requests;

public sealed record UpdateTenantUserRequest(
    Guid TenantId,
    Guid UserId,
    string FullName,
    string Role,
    bool IsActive,
    Guid ActingUserId);
