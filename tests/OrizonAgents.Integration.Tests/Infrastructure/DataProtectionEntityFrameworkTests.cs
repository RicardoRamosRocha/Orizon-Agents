using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure;

namespace OrizonAgents.Integration.Tests.DataProtection;

public sealed class DataProtectionEntityFrameworkTests
{
    [Fact]
    public void InfrastructureConfiguration_DoesNotRequireKeysPath()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=orizon_agents;Username=orizon;Password=test",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, addWebSecurity: false);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public void Provider_CanBeBuiltWithoutKeysPath_AndPersistsKeysInDbContext()
    {
        var services = new ServiceCollection();
        string databaseName = Guid.NewGuid().ToString();
        services.AddLogging();
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(
            provider => provider.GetRequiredService<CurrentTenant>());
        services.AddDbContext<OrizonAgentsDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddDataProtection()
            .SetApplicationName("OrizonAgents")
            .PersistKeysToDbContext<OrizonAgentsDbContext>();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        OrizonAgentsDbContext db =
            scope.ServiceProvider.GetRequiredService<OrizonAgentsDbContext>();
        db.Database.EnsureCreated();

        IDataProtector protector = scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("integration-test");
        IKeyManager keyManager = scope.ServiceProvider
            .GetRequiredService<IKeyManager>();
        keyManager.CreateNewKey(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(90));
        string protectedValue = protector.Protect("value");

        Assert.Equal("value", protector.Unprotect(protectedValue));
        using IServiceScope verificationScope = provider.CreateScope();
        OrizonAgentsDbContext verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<OrizonAgentsDbContext>();
        Assert.NotEmpty(keyManager.GetAllKeys());
        Assert.NotEmpty(verificationDb.Set<DataProtectionKey>());
        Assert.NotNull(
            verificationDb.Model.FindEntityType(typeof(DataProtectionKey)));
    }
}
