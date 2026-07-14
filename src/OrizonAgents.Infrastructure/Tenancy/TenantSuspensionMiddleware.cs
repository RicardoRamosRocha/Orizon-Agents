using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Tenants;

namespace OrizonAgents.Infrastructure.Tenancy;

public sealed class TenantSuspensionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantSuspensionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantManagementService tenantManagementService)
    {
        if (!ShouldCheck(context))
        {
            await _next(context);
            return;
        }

        string? tenantIdValue = context.User.FindFirstValue(OrizonClaimTypes.TenantId);
        if (Guid.TryParse(tenantIdValue, out Guid tenantId) &&
            await tenantManagementService.IsTenantSuspendedAsync(tenantId, context.RequestAborted))
        {
            context.Response.Redirect("/conta/organizacao-suspensa");
            return;
        }

        await _next(context);
    }

    private static bool ShouldCheck(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (context.User.IsInRole(OrizonRoles.PlatformAdmin))
        {
            return false;
        }

        PathString path = context.Request.Path;
        return !path.StartsWithSegments("/conta/sair") &&
            !path.StartsWithSegments("/conta/organizacao-suspensa") &&
            !path.StartsWithSegments("/css") &&
            !path.StartsWithSegments("/js") &&
            !path.StartsWithSegments("/lib") &&
            !path.StartsWithSegments("/favicon.ico");
    }
}
