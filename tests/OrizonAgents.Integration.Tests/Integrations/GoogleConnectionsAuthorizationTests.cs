using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Web.Controllers;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class GoogleConnectionsAuthorizationTests
{
    [Fact]
    public void OAuthActions_RequireTenantAdmin_AndMutationsRequirePostAndAntiforgery()
    {
        var type = typeof(GoogleConnectionsController);
        Assert.Equal("TenantAdminOnly", Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Policy);
        Assert.Empty(type.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        foreach (string action in new[] { "Connect", "UpgradeGmailRead", "Disconnect" })
        {
            var method = type.GetMethod(action)!;
            Assert.True(method.IsDefined(typeof(HttpPostAttribute), true));
            Assert.True(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true));
        }
        Assert.True(type.GetMethod("Callback")!.IsDefined(typeof(HttpGetAttribute), true));
        var upgrade = type.GetMethod("UpgradeGmailRead")!;
        Assert.Equal(new[] { typeof(Guid), typeof(CancellationToken) },
            upgrade.GetParameters().Select(x => x.ParameterType).ToArray());
        Assert.Equal("{id:guid}/google/ampliar/gmail-leitura",
            Assert.Single(upgrade.GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>()).Template);
        Assert.True(Assert.Single(type.GetCustomAttributes(typeof(ResponseCacheAttribute), true).Cast<ResponseCacheAttribute>()).NoStore);
    }
}
