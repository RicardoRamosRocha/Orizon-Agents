using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using OrizonAgents.API.Contracts.Agents;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.API.Security;

public static class AgentApiRateLimitDefaults
{
    public const string PolicyName = "AgentApiKeyPerCredential";
    public const int PermitLimit = 30;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}

public static class AgentApiRateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddAgentApiRateLimiting(
        this IServiceCollection services)
    {
        return services.AddAgentApiRateLimiting(
            AgentApiRateLimitDefaults.PermitLimit,
            AgentApiRateLimitDefaults.Window);
    }

    public static IServiceCollection AddAgentApiRateLimiting(
        this IServiceCollection services,
        int permitLimit,
        TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(window),
                "A janela do rate limiter deve ser positiva.");
        }

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
            options.AddPolicy(
                AgentApiRateLimitDefaults.PolicyName,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));
        });
    }

    private static string GetPartitionKey(HttpContext context)
    {
        string? value = context.User.FindFirstValue(
            OrizonClaimTypes.CredentialId);

        return Guid.TryParse(value, out Guid credentialId)
            ? $"credential:{credentialId:N}"
            : "unauthenticated";
    }

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter))
        {
            int retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new AgentApiErrorResponse(
                Success: false,
                new AgentApiError(
                    "rate_limit_exceeded",
                    "Limite de requisições excedido. Tente novamente em instantes.")),
            cancellationToken);
    }
}
