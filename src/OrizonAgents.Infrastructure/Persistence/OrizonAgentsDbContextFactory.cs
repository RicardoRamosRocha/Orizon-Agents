using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OrizonAgents.Application.Common.Tenancy;

namespace OrizonAgents.Infrastructure.Persistence;

public sealed class OrizonAgentsDbContextFactory : IDesignTimeDbContextFactory<OrizonAgentsDbContext>
{
    private const string DevelopmentConnectionString =
        "Host=localhost;Port=5432;Database=orizon_agents;Username=orizon;Password=orizon_dev_password";

    public OrizonAgentsDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ORIZON_CONNECTIONSTRINGS__DEFAULTCONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? DevelopmentConnectionString;

        var options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(OrizonAgentsDbContext).Assembly.FullName))
            .Options;

        return new OrizonAgentsDbContext(options, NoTenantContext.Instance);
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
