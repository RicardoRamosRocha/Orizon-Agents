using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Integration.Tests.Authentication;

namespace OrizonAgents.Integration.Tests.Dashboards;

public class PostLoginRedirectTests
{
    [Fact]
    public async Task GetPostLoginPathAsync_RedirectsPlatformAdminToPlatformDashboard()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var accountService = provider.GetRequiredService<IAccountService>();
        await CreateUserAsync(userManager, "platform@orizon.test", OrizonRoles.PlatformAdmin);

        string path = await accountService.GetPostLoginPathAsync("platform@orizon.test");

        Assert.Equal("/Platform/Dashboard", path);
    }

    [Fact]
    public async Task GetPostLoginPathAsync_RedirectsTenantAdminToAdminDashboard()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var accountService = provider.GetRequiredService<IAccountService>();
        await CreateUserAsync(userManager, "tenant-admin@orizon.test", OrizonRoles.TenantAdmin);

        string path = await accountService.GetPostLoginPathAsync("tenant-admin@orizon.test");

        Assert.Equal("/Admin/Dashboard", path);
    }

    [Fact]
    public async Task GetPostLoginPathAsync_RedirectsTenantMemberToHome()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var accountService = provider.GetRequiredService<IAccountService>();
        await CreateUserAsync(userManager, "member@orizon.test", OrizonRoles.TenantMember);

        string path = await accountService.GetPostLoginPathAsync("member@orizon.test");

        Assert.Equal("/inicio", path);
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Redirect User",
            TenantId = role == OrizonRoles.PlatformAdmin ? null : Guid.NewGuid(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(user, AuthenticationTestFixture.ValidPassword);
        Assert.True(result.Succeeded);
        await userManager.AddToRoleAsync(user, role);
    }
}
