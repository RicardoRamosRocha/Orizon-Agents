namespace OrizonAgents.Application.Accounts.Requests;

public sealed record CreateTenantUserRequest(
    Guid TenantId,
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword,
    string Role);
