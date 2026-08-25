using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Models;

namespace OrizonAgents.API.Security;

public sealed class ApiKeyAuthenticationMiddleware
{
    public const string HeaderName = "X-Orizon-Api-Key";

    private readonly RequestDelegate _next;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IApiCredentialService credentialService,
        ITenantContextSetter tenantContextSetter)
    {
        if (!context.Request.Path.StartsWithSegments("/api/agents"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(
                HeaderName,
                out var headerValues))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API key não informada."
            });
            return;
        }

        string apiKey = headerValues.ToString().Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API key inválida."
            });
            return;
        }

        ResolvedApiCredential? credential =
            await credentialService.ResolveAsync(
                apiKey,
                context.RequestAborted);

        if (credential is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "API key inválida."
            });
            return;
        }

        tenantContextSetter.SetTenantId(credential.TenantId);

        await _next(context);
    }
}
