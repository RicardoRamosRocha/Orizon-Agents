using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Tenancy;

namespace OrizonAgents.Infrastructure.Tenancy;

public sealed class CurrentTenantMiddleware
{
    private readonly RequestDelegate _next;

    public CurrentTenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextSetter tenantContextSetter)
    {
        string? tenantIdValue = context.User.FindFirstValue(OrizonClaimTypes.TenantId);
        if (Guid.TryParse(tenantIdValue, out Guid tenantId))
        {
            tenantContextSetter.SetTenantId(tenantId);
        }
        else
        {
            tenantContextSetter.Clear();
        }

        await _next(context);
    }
}
