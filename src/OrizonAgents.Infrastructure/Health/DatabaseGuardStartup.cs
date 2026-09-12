using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrizonAgents.Infrastructure.Health;

public static class DatabaseGuardStartup
{
    public static async Task ValidateAsync(
        IServiceProvider rootServices,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        DatabaseGuardOptions options = rootServices
            .GetRequiredService<IOptions<DatabaseGuardOptions>>()
            .Value;

        if (!environment.IsDevelopment() || !options.Enabled)
        {
            return;
        }

        using IServiceScope scope = rootServices.CreateScope();
        DatabaseIdentityValidator validator = scope.ServiceProvider
            .GetRequiredService<DatabaseIdentityValidator>();
        DatabaseIdentityResult result = await validator.ValidateAsync(cancellationToken);

        if (options.FailOnPendingMigrations && result.PendingMigrations.Count > 0)
        {
            throw new InvalidOperationException(
                "Database has pending migrations. Apply them explicitly before starting Orizon Agents: "
                + string.Join(", ", result.PendingMigrations));
        }

        scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("OrizonAgents.DatabaseGuard")
            .LogInformation(
                "Database identity validated. Environment={Environment} Provider={Provider} Host={Host} Port={Port} Database={Database} ServerVersion={ServerVersion} PendingMigrations={PendingMigrations}",
                environment.EnvironmentName,
                result.Provider,
                result.Host,
                result.Port,
                result.Database,
                result.ServerVersion,
                result.PendingMigrations.Count);
    }
}
