using Microsoft.AspNetCore.Builder;

namespace OrizonAgents.Infrastructure.Tenancy;

public static class TenancyApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCurrentTenant(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CurrentTenantMiddleware>();
    }
}
