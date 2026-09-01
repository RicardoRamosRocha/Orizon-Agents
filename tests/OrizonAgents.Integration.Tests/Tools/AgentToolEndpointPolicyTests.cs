using Microsoft.Extensions.Options;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class AgentToolEndpointPolicyTests
{
    [Fact]
    public async Task IsAllowedAsync_BlocksUnsupportedScheme()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("file:///etc/passwd"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_BlocksPublicHttp()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("http://example.com/status"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_BlocksCredentialsInUrl()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("https://user:password@example.com/status"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_BlocksLocalhostByDefault()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("http://localhost:5000/status"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_AllowsLocalhostWhenExplicitlyEnabled()
    {
        var policy = CreatePolicy(
            allowLocalhost: true);

        bool allowed = await policy.IsAllowedAsync(
            new Uri("http://localhost:5000/status"));

        Assert.True(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_BlocksPrivateIpv4()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("https://192.168.1.10/status"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_BlocksLinkLocalIpv4()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("https://169.254.169.254/latest/meta-data"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_BlocksIpv6Loopback()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("https://[::1]/status"));

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_AllowsPublicHttps()
    {
        var policy = CreatePolicy();

        bool allowed = await policy.IsAllowedAsync(
            new Uri("https://example.com/status"));

        Assert.True(allowed);
    }

    private static AgentToolEndpointPolicy CreatePolicy(
        bool allowLocalhost = false,
        bool allowPrivateNetworks = false)
    {
        var options = Options.Create(
            new AgentToolHttpOptions
            {
                AllowLocalhost = allowLocalhost,
                AllowPrivateNetworks = allowPrivateNetworks
            });

        return new AgentToolEndpointPolicy(options);
    }
}
