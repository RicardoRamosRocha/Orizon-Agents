using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using OrizonAgents.API.Security;
using OrizonAgents.Application.Common.Security;

namespace OrizonAgents.Integration.Tests.Api;

public class AgentApiRateLimitingTests
{
    [Fact]
    public void ProductionPolicy_IsThirtyRequestsPerMinuteWithNoQueue()
    {
        Assert.Equal(30, AgentApiRateLimitDefaults.PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(1), AgentApiRateLimitDefaults.Window);
    }

    [Fact]
    public async Task CredentialAAndCredentialB_HaveIndependentQuotas()
    {
        await using ServiceProvider provider = CreateProvider(permitLimit: 2);
        RequestDelegate pipeline = CreatePipeline(provider);
        Guid credentialA = Guid.NewGuid();
        Guid credentialB = Guid.NewGuid();

        Assert.Equal(StatusCodes.Status200OK, (await SendAsync(pipeline, provider, credentialA)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await SendAsync(pipeline, provider, credentialA)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await SendAsync(pipeline, provider, credentialA)).Response.StatusCode);

        Assert.Equal(StatusCodes.Status200OK, (await SendAsync(pipeline, provider, credentialB)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, (await SendAsync(pipeline, provider, credentialB)).Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await SendAsync(pipeline, provider, credentialB)).Response.StatusCode);
    }

    [Fact]
    public async Task RequestAboveLimit_ReturnsSafeEnvelopeAndRetryAfter()
    {
        await using ServiceProvider provider = CreateProvider(permitLimit: 1);
        RequestDelegate pipeline = CreatePipeline(provider);
        Guid credentialId = Guid.NewGuid();
        DefaultHttpContext accepted = await SendAsync(pipeline, provider, credentialId);
        DefaultHttpContext rejected = await SendAsync(pipeline, provider, credentialId);

        Assert.Equal(StatusCodes.Status200OK, accepted.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, rejected.Response.StatusCode);
        Assert.True(rejected.Response.Headers.TryGetValue("Retry-After", out var retryAfter));
        Assert.True(int.TryParse(retryAfter, out int seconds));
        Assert.True(seconds > 0);

        string json = await ReadBodyAsync(rejected);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(
            "rate_limit_exceeded",
            root.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("api", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(credentialId.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EndpointWithoutPolicy_IsNotRateLimited()
    {
        await using ServiceProvider provider = CreateProvider(permitLimit: 1);
        RequestDelegate pipeline = CreatePipeline(provider);
        Guid credentialId = Guid.NewGuid();
        await SendAsync(pipeline, provider, credentialId);
        await SendAsync(pipeline, provider, credentialId);

        DefaultHttpContext unrelatedEndpoint = await SendAsync(
            pipeline,
            provider,
            credentialId,
            enablePolicy: false);

        Assert.Equal(
            StatusCodes.Status200OK,
            unrelatedEndpoint.Response.StatusCode);
    }

    private static ServiceProvider CreateProvider(int permitLimit)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAgentApiRateLimiting(
            permitLimit,
            TimeSpan.FromMinutes(1));
        return services.BuildServiceProvider();
    }

    private static RequestDelegate CreatePipeline(IServiceProvider provider)
    {
        var application = new ApplicationBuilder(provider);
        application.UseRateLimiter();
        application.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        return application.Build();
    }

    private static async Task<DefaultHttpContext> SendAsync(
        RequestDelegate pipeline,
        IServiceProvider provider,
        Guid credentialId,
        bool enablePolicy = true)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            Response =
            {
                Body = new MemoryStream()
            },
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(
                    OrizonClaimTypes.CredentialId,
                    credentialId.ToString())
            ], AgentApiKeyDefaults.AuthenticationScheme))
        };

        object[] metadata = enablePolicy
            ? [new EnableRateLimitingAttribute(AgentApiRateLimitDefaults.PolicyName)]
            : [];
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata),
            "Test endpoint"));

        await pipeline(context);
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(
            context.Response.Body,
            leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
