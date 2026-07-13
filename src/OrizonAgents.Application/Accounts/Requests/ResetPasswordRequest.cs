namespace OrizonAgents.Application.Accounts.Requests;

public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string Password,
    string ConfirmPassword);
