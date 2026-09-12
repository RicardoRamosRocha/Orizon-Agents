using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Health;

public sealed record DatabaseIdentityResult(
    string Provider,
    string Host,
    int Port,
    string Database,
    string ServerVersion,
    IReadOnlyList<string> PendingMigrations);

public sealed class DatabaseIdentityValidator
{
    private readonly IConfiguration _configuration;
    private readonly DatabaseGuardOptions _options;
    private readonly IDbContextFactory<OrizonAgentsDbContext> _dbContextFactory;

    public DatabaseIdentityValidator(
        IConfiguration configuration,
        IOptions<DatabaseGuardOptions> options,
        IDbContextFactory<OrizonAgentsDbContext> dbContextFactory)
    {
        _configuration = configuration;
        _options = options.Value;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<DatabaseIdentityResult> ValidateAsync(CancellationToken cancellationToken)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required for database identity validation.");

        NpgsqlConnectionStringBuilder connection = new(connectionString);
        string host = connection.Host ?? string.Empty;
        int port = connection.Port;
        string database = connection.Database ?? string.Empty;

        if (!string.Equals(host, _options.ExpectedHost, StringComparison.OrdinalIgnoreCase)
            || port != _options.ExpectedPort
            || !string.Equals(database, _options.ExpectedDatabase, StringComparison.Ordinal))
        {
            throw UnexpectedConfiguration(host, port, database);
        }

        await using var sqlConnection = new NpgsqlConnection(connection.ConnectionString);
        await sqlConnection.OpenAsync(cancellationToken);

        await using NpgsqlCommand command = sqlConnection.CreateCommand();
        command.CommandText = "SELECT current_database(), current_setting('server_version_num'), version();";
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Database identity query returned no result.");
        }

        string actualDatabase = reader.GetString(0);
        string serverVersionNumber = reader.GetString(1);
        string serverVersion = reader.GetString(2);
        int serverMajorVersion = int.Parse(serverVersionNumber[..Math.Min(2, serverVersionNumber.Length)]);

        if (!string.Equals(actualDatabase, _options.ExpectedDatabase, StringComparison.Ordinal)
            || serverMajorVersion != _options.ExpectedPostgresMajorVersion)
        {
            throw new InvalidOperationException(
                "Orizon Agents está conectado a uma configuração de banco inesperada. "
                + $"Esperado: PostgreSQL {_options.ExpectedPostgresMajorVersion} / "
                + $"{_options.ExpectedHost}:{_options.ExpectedPort} / {_options.ExpectedDatabase}. "
                + $"Recebido: PostgreSQL {serverMajorVersion} / {host}:{port} / {actualDatabase}.");
        }

        await reader.CloseAsync();

        await using OrizonAgentsDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        IReadOnlyList<string> pendingMigrations =
            (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        return new DatabaseIdentityResult(
            "PostgreSQL",
            host,
            port,
            actualDatabase,
            serverVersion,
            pendingMigrations);
    }

    private InvalidOperationException UnexpectedConfiguration(string host, int port, string database)
    {
        return new InvalidOperationException(
            "Orizon Agents está conectado a uma configuração de banco inesperada. "
            + $"Esperado: PostgreSQL {_options.ExpectedPostgresMajorVersion} / "
            + $"{_options.ExpectedHost}:{_options.ExpectedPort} / {_options.ExpectedDatabase}. "
            + $"Recebido: PostgreSQL / {host}:{port} / {database}.");
    }
}
