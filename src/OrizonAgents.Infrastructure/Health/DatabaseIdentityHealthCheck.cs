using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace OrizonAgents.Infrastructure.Health;

public sealed class DatabaseIdentityHealthCheck : IHealthCheck
{
    private readonly DatabaseIdentityValidator _validator;
    private readonly DatabaseGuardOptions _options;

    public DatabaseIdentityHealthCheck(
        DatabaseIdentityValidator validator,
        IOptions<DatabaseGuardOptions> options)
    {
        _validator = validator;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Database identity guard is disabled for this environment.");
        }

        try
        {
            DatabaseIdentityResult result = await _validator.ValidateAsync(cancellationToken);
            if (_options.FailOnPendingMigrations && result.PendingMigrations.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"{result.PendingMigrations.Count} pending migration(s).",
                    data: new Dictionary<string, object>
                    {
                        ["pendingMigrations"] = result.PendingMigrations.Count
                    });
            }

            return HealthCheckResult.Healthy(
                "Database identity and migrations are valid.",
                data: new Dictionary<string, object>
                {
                    ["provider"] = result.Provider,
                    ["host"] = result.Host,
                    ["port"] = result.Port,
                    ["database"] = result.Database,
                    ["serverVersion"] = result.ServerVersion,
                    ["pendingMigrations"] = result.PendingMigrations.Count
                });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database identity validation failed.", exception);
        }
    }
}
