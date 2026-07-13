namespace OrizonAgents.Application.Accounts.Requests;

public sealed record ChangePasswordRequest(
    Guid UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);
