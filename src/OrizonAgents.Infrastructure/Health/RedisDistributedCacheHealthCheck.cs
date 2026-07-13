using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrizonAgents.Infrastructure.Health;

public sealed class RedisDistributedCacheHealthCheck : IHealthCheck
{
    private static readonly byte[] Payload = [1];
    private readonly IDistributedCache _cache;

    public RedisDistributedCacheHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        const string key = "orizon:health";

        try
        {
            await _cache.SetAsync(
                key,
                Payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                },
                cancellationToken);

            await _cache.RemoveAsync(key, cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis distributed cache is unavailable.", exception);
        }
    }
}
