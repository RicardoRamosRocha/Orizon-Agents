using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Integration.Tests.Authentication;

public class AccountServiceTests
{
    [Fact]
    public async Task RegisterOrganizationAsync_CreatesTenantSettingsUserAndTenantAdmin()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IAccountService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        OperationResult<Guid> result = await service.RegisterOrganizationAsync(CreateRegisterRequest());

        Assert.True(result.Succeeded, result.FirstError);
        Assert.Single(dbContext.Tenants);
        Assert.Single(dbContext.TenantSettings);

        ApplicationUser user = (await userManager.FindByEmailAsync("admin@orizon.test"))!;
        Assert.NotNull(user);
        Assert.NotNull(user.TenantId);
        Assert.True(await userManager.IsInRoleAsync(user, OrizonRoles.TenantAdmin));
    }

    [Fact]
    public async Task RegisterOrganizationAsync_WithDuplicateEmail_DoesNotCreateSecondTenant()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IAccountService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();

        await service.RegisterOrganizationAsync(CreateRegisterRequest());
        OperationResult<Guid> duplicate = await service.RegisterOrganizationAsync(
            CreateRegisterRequest(organizationName: "Outra", slug: "outra"));

        Assert.False(duplicate.Succeeded);
        Assert.Single(dbContext.Tenants);
    }

    [Fact]
    public async Task PasswordSignInAsync_WithActiveUser_Succeeds()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IAccountService>();
        await service.RegisterOrganizationAsync(CreateRegisterRequest());

        OperationResult result = await service.PasswordSignInAsync(
            new LoginRequest("admin@orizon.test", AuthenticationTestFixture.ValidPassword, false));

        Assert.True(result.Succeeded, result.FirstError);
    }

    [Fact]
    public async Task PasswordSignInAsync_WithInactiveUser_FailsWithoutEnumeration()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IAccountService>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        await service.RegisterOrganizationAsync(CreateRegisterRequest());
        ApplicationUser user = (await userManager.FindByEmailAsync("admin@orizon.test"))!;
        user.IsActive = false;
        await userManager.UpdateAsync(user);

        OperationResult result = await service.PasswordSignInAsync(
            new LoginRequest("admin@orizon.test", AuthenticationTestFixture.ValidPassword, false));

        Assert.False(result.Succeeded);
        Assert.Contains("inativa", result.FirstError);
    }

    [Fact]
    public async Task PlatformAdmin_CanExistWithoutTenant()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "platform@orizon.test",
            Email = "platform@orizon.test",
            EmailConfirmed = true,
            FullName = "Platform Admin",
            TenantId = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult created = await userManager.CreateAsync(user, AuthenticationTestFixture.ValidPassword);
        await userManager.AddToRoleAsync(user, OrizonRoles.PlatformAdmin);

        Assert.True(created.Succeeded);
        Assert.Null(user.TenantId);
        Assert.True(await userManager.IsInRoleAsync(user, OrizonRoles.PlatformAdmin));
    }

    private static RegisterOrganizationRequest CreateRegisterRequest(
        string organizationName = "Orizon Test",
        string slug = "orizon-test")
    {
        return new RegisterOrganizationRequest(
            organizationName,
            slug,
            "Admin Orizon",
            "admin@orizon.test",
            AuthenticationTestFixture.ValidPassword,
            AuthenticationTestFixture.ValidPassword,
            true);
    }
}
