using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Integration.Tests.Persistence;

public class OrizonAgentsDbContextModelTests
{
    [Fact]
    public void DbContext_UsesNpgsqlProvider()
    {
        using OrizonAgentsDbContext context = CreateContext();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [Fact]
    public void TenantSlug_HasUniqueIndex()
    {
        using OrizonAgentsDbContext context = CreateContext();

        IEntityType tenantEntity = context.Model.FindEntityType(typeof(Tenant))!;
        IProperty slugProperty = tenantEntity.FindProperty(nameof(Tenant.Slug))!;
        IIndex slugIndex = tenantEntity.GetIndexes().Single(index => index.Properties.Contains(slugProperty));

        Assert.True(slugIndex.IsUnique);
    }

    [Fact]
    public void TenantSettings_HasOneToOneTenantRelationship()
    {
        using OrizonAgentsDbContext context = CreateContext();

        IEntityType settingsEntity = context.Model.FindEntityType(typeof(TenantSettings))!;
        IForeignKey tenantForeignKey = settingsEntity.GetForeignKeys().Single(
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Tenant));

        Assert.True(tenantForeignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Cascade, tenantForeignKey.DeleteBehavior);
        Assert.Equal(nameof(TenantSettings.TenantId), tenantForeignKey.Properties.Single().Name);
    }

    [Fact]
    public void TenantSettings_HasTenantQueryFilter()
    {
        using OrizonAgentsDbContext context = CreateContext();

        IEntityType settingsEntity = context.Model.FindEntityType(typeof(TenantSettings))!;

        Assert.NotNull(settingsEntity.GetQueryFilter());
    }

    private static OrizonAgentsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseNpgsql("Host=localhost;Database=orizon_agents_tests;Username=orizon;Password=orizon_dev_password")
            .Options;

        return new OrizonAgentsDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ICurrentTenant
    {
        public Guid? TenantId => null;

        public bool HasTenant => false;
    }
}
