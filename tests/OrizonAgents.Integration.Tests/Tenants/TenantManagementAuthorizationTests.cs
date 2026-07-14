using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Web.Controllers;

namespace OrizonAgents.Integration.Tests.Tenants;

public class TenantManagementAuthorizationTests
{
    [Fact]
    public void PlatformTenantsController_RequiresPlatformAdminPolicy()
    {
        AuthorizeAttribute attribute = typeof(PlatformTenantsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("PlatformAdminOnly", attribute.Policy);
    }

    [Fact]
    public void OrganizationPost_RequiresTenantAdminPolicyAndAntiforgery()
    {
        var method = typeof(OrganizationController).GetMethods()
            .Single(candidate =>
                candidate.Name == nameof(OrganizationController.Index) &&
                candidate.GetParameters().Length == 2);

        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("TenantAdminOnly", authorize.Policy);
        Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public void TenantMutationActions_UsePostAndAntiforgery()
    {
        string[] actionNames = ["Create", "Edit", "Suspend", "Reactivate"];

        foreach (string actionName in actionNames)
        {
            var methods = typeof(PlatformTenantsController).GetMethods()
                .Where(method => method.Name == actionName)
                .Where(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Any())
                .ToArray();

            Assert.NotEmpty(methods);
            Assert.All(methods, method =>
                Assert.NotNull(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault()));
        }
    }
}
