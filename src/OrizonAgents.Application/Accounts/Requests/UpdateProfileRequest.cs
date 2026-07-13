namespace OrizonAgents.Application.Accounts.Requests;

public sealed record UpdateProfileRequest(Guid UserId, string FullName);
