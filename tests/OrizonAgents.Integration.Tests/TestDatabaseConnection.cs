using Npgsql;

namespace OrizonAgents.Integration.Tests;

internal static class TestDatabaseConnection
{
    public static string For(string database)
    {
        string? configured = Environment.GetEnvironmentVariable("ORIZON_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return $"Host=127.0.0.1;Port=55432;Database={database};Username=orizon";
        }

        var builder = new NpgsqlConnectionStringBuilder(configured)
        {
            Host = "127.0.0.1",
            Port = 55432,
            Database = database
        };
        return builder.ConnectionString;
    }
}
