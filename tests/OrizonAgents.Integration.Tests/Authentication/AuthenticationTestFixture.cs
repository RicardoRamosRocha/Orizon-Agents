using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Common.Email;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Infrastructure.Accounts;
using OrizonAgents.Application.Dashboards;
using OrizonAgents.Infrastructure.Dashboards;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.Tenants;

namespace OrizonAgents.Integration.Tests.Authentication;

internal static class AuthenticationTestFixture
{
    public const string ValidPassword = "Senha@12345";

    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddHttpContextAccessor();
        services.AddDataProtection()
            .PersistKeysToFileSystem(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "orizon-agents-test-keys")));
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddScoped<IEmailSender, TestEmailSender>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITenantUserService, TenantUserService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddDbContext<OrizonAgentsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<OrizonAgentsDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        SeedRolesAsync(provider).GetAwaiter().GetResult();
        return provider;
    }

    private static async Task SeedRolesAsync(ServiceProvider provider)
    {
        RoleManager<ApplicationRole> roleManager = provider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (string role in OrizonRoles.All)
        {
            await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public Task SendAccountLinkAsync(
            string email,
            string subject,
            string safeLink,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
