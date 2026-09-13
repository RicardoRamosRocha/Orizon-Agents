using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OrizonAgents.Infrastructure.Health;

public sealed class DatabaseGuardHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly DatabaseGuardOptions _options;
    private readonly ILogger<DatabaseGuardHostedService> _logger;

    public DatabaseGuardHostedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IOptions<DatabaseGuardOptions> options,
        ILogger<DatabaseGuardHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() || !_options.Enabled)
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        DatabaseIdentityValidator validator = scope.ServiceProvider
            .GetRequiredService<DatabaseIdentityValidator>();
        DatabaseIdentityResult result = await validator.ValidateAsync(cancellationToken);

        if (_options.FailOnPendingMigrations && result.PendingMigrations.Count > 0)
        {
            throw new InvalidOperationException(
                "Database has pending migrations. Apply them explicitly before starting Orizon Agents: "
                + string.Join(", ", result.PendingMigrations));
        }

        _logger.LogInformation(
            "Database identity validated. Environment={Environment} Provider={Provider} Host={Host} Port={Port} Database={Database} ServerVersion={ServerVersion} PendingMigrations={PendingMigrations}",
            _environment.EnvironmentName,
            result.Provider,
            result.Host,
            result.Port,
            result.Database,
            result.ServerVersion,
            result.PendingMigrations.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
