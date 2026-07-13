using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Users;

namespace OrizonAgents.Infrastructure.Users;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => ReadGuid(OrizonClaimTypes.UserId)
        ?? ReadGuid(ClaimTypes.NameIdentifier);

    public Guid? TenantId => ReadGuid(OrizonClaimTypes.TenantId);

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
    }

    private Guid? ReadGuid(string claimType)
    {
        string? value = _httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out Guid parsed) ? parsed : null;
    }
}
