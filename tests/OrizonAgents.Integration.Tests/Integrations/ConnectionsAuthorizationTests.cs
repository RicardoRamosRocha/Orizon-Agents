using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Web.Controllers;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class ConnectionsAuthorizationTests
{
    [Fact]
    public void Controller_RequiresTenantAdmin_AndAllMutationsRequireAntiforgery()
    {
        var controller = typeof(ConnectionsController);
        var authorize = Assert.Single(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("TenantAdminOnly", authorize.Policy);
        var mutations = controller.GetMethods()
            .Where(method => method.IsDefined(typeof(HttpPostAttribute), true)).ToArray();
        Assert.Equal(4, mutations.Length);
        Assert.All(mutations, method =>
            Assert.True(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true)));
    }
}