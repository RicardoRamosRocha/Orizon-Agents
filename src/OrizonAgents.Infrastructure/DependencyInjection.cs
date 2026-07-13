using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Health;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        string redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis connection string is required.");

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(provider => provider.GetRequiredService<CurrentTenant>());

        services.AddDbContext<OrizonAgentsDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(OrizonAgentsDbContext).Assembly.FullName));
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "orizon-agents:";
        });

        services.AddHealthChecks()
            .AddDbContextCheck<OrizonAgentsDbContext>(
                "postgresql",
                HealthStatus.Unhealthy)
            .AddCheck<RedisDistributedCacheHealthCheck>(
                "redis",
                HealthStatus.Unhealthy);

        return services;
    }
}
