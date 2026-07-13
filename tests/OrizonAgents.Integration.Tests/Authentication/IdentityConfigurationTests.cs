using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Infrastructure.Identity;

namespace OrizonAgents.Integration.Tests.Authentication;

public class IdentityConfigurationTests
{
    [Fact]
    public void IdentityOptions_RequireUniqueEmailAndStrongPassword()
    {
        using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        IdentityOptions options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.User.RequireUniqueEmail);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireNonAlphanumeric);
        Assert.True(options.Lockout.AllowedForNewUsers);
    }

    [Fact]
    public async Task ClaimsFactory_AddsUserAndTenantClaims()
    {
        using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var claimsFactory = provider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        Guid tenantId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            UserName = "claims@orizon.test",
            Email = "claims@orizon.test",
            FullName = "Claims User",
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow
        };
        await userManager.CreateAsync(user, AuthenticationTestFixture.ValidPassword);

        ClaimsPrincipal principal = await claimsFactory.CreateAsync(user);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(OrizonClaimTypes.UserId));
        Assert.Equal(tenantId.ToString(), principal.FindFirstValue(OrizonClaimTypes.TenantId));
        Assert.Equal("Claims User", principal.FindFirstValue(OrizonClaimTypes.FullName));
    }
}
