using Microsoft.AspNetCore.Authorization;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Web.Controllers;

namespace OrizonAgents.Integration.Tests.Dashboards;

public class DashboardAuthorizationTests
{
    [Fact]
    public void AdminDashboard_RequiresTenantAdminPolicy()
    {
        AuthorizeAttribute attribute = GetAuthorizeAttribute<AdminDashboardController>();

        Assert.Equal("TenantAdminOnly", attribute.Policy);
    }

    [Fact]
    public void PlatformDashboard_RequiresPlatformAdminPolicy()
    {
        AuthorizeAttribute attribute = GetAuthorizeAttribute<PlatformDashboardController>();

        Assert.Equal("PlatformAdminOnly", attribute.Policy);
    }

    [Fact]
    public void DashboardPolicies_DoNotGrantTenantMemberAdministrativeAccess()
    {
        Assert.NotEqual(OrizonRoles.TenantMember, OrizonRoles.TenantAdmin);
        Assert.NotEqual(OrizonRoles.TenantMember, OrizonRoles.PlatformAdmin);
    }

    private static AuthorizeAttribute GetAuthorizeAttribute<TController>()
    {
        return typeof(TController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
    }
}
