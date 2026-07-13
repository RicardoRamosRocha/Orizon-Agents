using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.Infrastructure.Identity;

public sealed class ApplicationUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(OrizonClaimTypes.UserId, user.Id.ToString()));
        identity.AddClaim(new Claim(OrizonClaimTypes.FullName, user.FullName));

        if (user.TenantId.HasValue)
        {
            identity.AddClaim(new Claim(OrizonClaimTypes.TenantId, user.TenantId.Value.ToString()));
        }

        return identity;
    }
}
