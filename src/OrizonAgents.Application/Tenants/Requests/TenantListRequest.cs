namespace OrizonAgents.Application.Tenants.Requests;

public sealed record TenantListRequest(
    string? Search,
    string? Status,
    string? Sort,
    int PageNumber = 1,
    int PageSize = 10);
