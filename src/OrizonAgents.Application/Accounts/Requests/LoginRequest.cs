namespace OrizonAgents.Application.Accounts.Requests;

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe);
