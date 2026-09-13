using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OrizonAgents.Application.Common.Tenancy;
using Pgvector.EntityFrameworkCore;

namespace OrizonAgents.Infrastructure.Persistence;

public sealed class OrizonAgentsDbContextFactory : IDesignTimeDbContextFactory<OrizonAgentsDbContext>
{
    public OrizonAgentsDbContext CreateDbContext(string[] args)
    {
        string? commandLineConnection = GetArgumentValue(args, "--connection");
        string connectionString = Environment.GetEnvironmentVariable("ORIZON_CONNECTIONSTRINGS__DEFAULTCONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? commandLineConnection
            ?? throw new InvalidOperationException(
                "Connection string 'ConnectionStrings__DefaultConnection' must be configured for EF design-time operations.");

        var options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsAssembly(typeof(OrizonAgentsDbContext).Assembly.FullName)
                    .UseVector())
            .Options;

        return new OrizonAgentsDbContext(options, NoTenantContext.Instance);
    }

    private static string? GetArgumentValue(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private sealed class NoTenantContext : ICurrentTenant
    {
        public static readonly NoTenantContext Instance = new();

        private NoTenantContext()
        {
        }

        public Guid? TenantId => null;

        public bool HasTenant => false;
    }
}
