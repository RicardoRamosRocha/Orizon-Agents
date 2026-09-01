using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.API.Contracts.Agents;
using OrizonAgents.API.Controllers;
using OrizonAgents.API.Security;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Application.Agents.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Models;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Authentication;

public class AgentApiKeyAuthenticationTests
{
    private const string ValidApiKey = "orizon_identifier.secret";

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CredentialId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public async Task MissingHeader_ChallengesWith401()
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);

        AuthenticateResult result = await context.AuthenticateAsync(
            AgentApiKeyDefaults.AuthenticationScheme);
        await context.ChallengeAsync(AgentApiKeyDefaults.AuthenticationScheme);

        Assert.True(result.None);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvalidKey_ChallengesWith401()
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);
        context.Request.Headers[AgentApiKeyDefaults.HeaderName] = "invalid";

        AuthenticateResult result = await context.AuthenticateAsync(
            AgentApiKeyDefaults.AuthenticationScheme);
        await context.ChallengeAsync(AgentApiKeyDefaults.AuthenticationScheme);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task RevokedKey_ChallengesWith401()
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);
        context.Request.Headers[AgentApiKeyDefaults.HeaderName] = "revoked";

        AuthenticateResult result = await context.AuthenticateAsync(
            AgentApiKeyDefaults.AuthenticationScheme);
        await context.ChallengeAsync(AgentApiKeyDefaults.AuthenticationScheme);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(AgentApiKeyDefaults.HeaderName)]
    [InlineData(AgentApiKeyDefaults.LegacyHeaderName)]
    public async Task ValidKey_AuthenticatesWithRequiredClaims(string headerName)
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);
        context.Request.Headers[headerName] = ValidApiKey;

        AuthenticateResult result = await context.AuthenticateAsync(
            AgentApiKeyDefaults.AuthenticationScheme);

        Assert.True(result.Succeeded);
        Assert.Equal(
            AgentApiKeyDefaults.AuthenticationScheme,
            result.Principal!.Identity!.AuthenticationType);
        Assert.Equal(TenantId.ToString(), result.Principal.FindFirstValue(OrizonClaimTypes.TenantId));
        Assert.Equal(CredentialId.ToString(), result.Principal.FindFirstValue(OrizonClaimTypes.CredentialId));
        Assert.Equal(AgentId.ToString(), result.Principal.FindFirstValue(OrizonClaimTypes.AgentId));
    }

    [Fact]
    public async Task MatchingOfficialAndLegacyHeaders_Authenticate()
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);
        context.Request.Headers[AgentApiKeyDefaults.HeaderName] = ValidApiKey;
        context.Request.Headers[AgentApiKeyDefaults.LegacyHeaderName] = ValidApiKey;

        AuthenticateResult result = await context.AuthenticateAsync(
            AgentApiKeyDefaults.AuthenticationScheme);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ConflictingHeaders_ChallengeWith401()
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);
        context.Request.Headers[AgentApiKeyDefaults.HeaderName] = ValidApiKey;
        context.Request.Headers[AgentApiKeyDefaults.LegacyHeaderName] = "different";

        AuthenticateResult result = await context.AuthenticateAsync(
            AgentApiKeyDefaults.AuthenticationScheme);
        await context.ChallengeAsync(AgentApiKeyDefaults.AuthenticationScheme);

        Assert.False(result.Succeeded);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedClaim_EstablishesCurrentTenant()
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext context = CreateContext(provider);
        context.Request.Headers[AgentApiKeyDefaults.HeaderName] = ValidApiKey;
        var currentTenant = provider.GetRequiredService<CurrentTenant>();
        Guid? tenantSeenByEndpoint = null;
        var application = new ApplicationBuilder(provider);
        application.UseAuthentication();
        application.UseCurrentTenant();
        application.Run(_ =>
        {
            tenantSeenByEndpoint = currentTenant.TenantId;
            return Task.CompletedTask;
        });

        await application.Build()(context);

        Assert.True(context.User.Identity!.IsAuthenticated);
        Assert.Equal(TenantId, tenantSeenByEndpoint);
    }

    [Fact]
    public async Task AgentA_TryingAgentB_Returns403WithoutCallingRunner()
    {
        Guid agentB = Guid.NewGuid();
        var runner = new StubAgentRunner();

        int statusCode = await ExecuteControllerAsync(
            runner,
            CreatePrincipal(TenantId, CredentialId, AgentId),
            agentB);

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(runner.WasCalled);
    }

    [Fact]
    public async Task TenantAKey_CannotRunAgentFromAnotherTenant()
    {
        Guid tenantBAgent = Guid.NewGuid();
        var runner = new StubAgentRunner();

        int statusCode = await ExecuteControllerAsync(
            runner,
            CreatePrincipal(TenantId, CredentialId, AgentId),
            tenantBAgent);

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(runner.WasCalled);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CurrentTenant>();
        services.AddSingleton<ICurrentTenant>(provider =>
            provider.GetRequiredService<CurrentTenant>());
        services.AddSingleton<ITenantContextSetter>(provider =>
            provider.GetRequiredService<CurrentTenant>());
        services.AddSingleton<IApiCredentialService>(
            new StubApiCredentialService(apiKey =>
                apiKey == ValidApiKey
                    ? new ResolvedApiCredential(
                        CredentialId,
                        TenantId,
                        AgentId,
                        "identifier",
                        "Test")
                    : null));
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    AgentApiKeyDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, AgentApiKeyAuthenticationHandler>(
                AgentApiKeyDefaults.AuthenticationScheme,
                _ => { });

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider provider)
    {
        return new DefaultHttpContext
        {
            RequestServices = provider
        };
    }

    private static ClaimsPrincipal CreatePrincipal(
        Guid tenantId,
        Guid credentialId,
        Guid agentId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrizonClaimTypes.TenantId, tenantId.ToString()),
            new Claim(OrizonClaimTypes.CredentialId, credentialId.ToString()),
            new Claim(OrizonClaimTypes.AgentId, agentId.ToString())
        ], AgentApiKeyDefaults.AuthenticationScheme));
    }

    private static async Task<int> ExecuteControllerAsync(
        IAiAgentRunner runner,
        ClaimsPrincipal principal,
        Guid routeAgentId)
    {
        await using ServiceProvider provider = CreateProvider();
        DefaultHttpContext httpContext = CreateContext(provider);
        httpContext.User = principal;
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor());
        var controller = new AgentsController(
            runner,
            new StubAiAgentService(),
            NullLogger<AgentsController>.Instance)
        {
            ControllerContext = new ControllerContext(actionContext)
        };

        IActionResult result = await controller.Run(
            routeAgentId,
            new RunAgentRequest("Olá"),
            CancellationToken.None);

        return Assert.IsType<ObjectResult>(result).StatusCode
            ?? StatusCodes.Status200OK;
    }

    private sealed class StubApiCredentialService : IApiCredentialService
    {
        public Task<IReadOnlyList<ApiCredentialListItem>> ListAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private readonly Func<string, ResolvedApiCredential?> _resolve;

        public StubApiCredentialService(Func<string, ResolvedApiCredential?> resolve)
        {
            _resolve = resolve;
        }

        public Task<ResolvedApiCredential?> ResolveAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_resolve(apiKey));
        }

        public Task<CreatedApiCredential> CreateAsync(Guid tenantId, Guid agentId, string name, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RevokeAsync(Guid tenantId, Guid credentialId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CreatedApiCredential> RegenerateAsync(Guid tenantId, Guid credentialId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CreatedApiCredential> CreateAsync(Guid tenantId, string name, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubAgentRunner : IAiAgentRunner
    {
        public bool WasCalled { get; private set; }

        public Task<OperationResult<AiAgentRunResult>> RunAsync(
            Guid agentId,
            AgentRunRequest request,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("O runner não deveria ser chamado.");
        }
    }

    private sealed class StubAiAgentService : IAiAgentService
    {
        public Task<AiAgentDetailsDto?> GetAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiAgentDetailsDto?>(null);

        public Task<IReadOnlyList<AiAgentListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<Guid>> CreateAsync(CreateAiAgentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> UpdateAsync(UpdateAiAgentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> ActivateAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<OperationResult> DeactivateAsync(Guid agentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
