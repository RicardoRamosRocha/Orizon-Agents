namespace OrizonAgents.Application.Accounts.Requests;

public sealed record RegisterOrganizationRequest(
    string OrganizationName,
    string Slug,
    string FullName,
    string Email,
    string Password,
    string ConfirmPassword,
    bool AcceptedTerms);
