using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Application.Integrations.Models;

namespace OrizonAgents.API.Security;

public sealed class AgentApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiCredentialService _credentialService;

    public AgentApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiCredentialService credentialService)
        : base(options, logger, encoder)
    {
        _credentialService = credentialService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        HeaderValue official = ReadHeader(AgentApiKeyDefaults.HeaderName);
        HeaderValue legacy = ReadHeader(AgentApiKeyDefaults.LegacyHeaderName);

        if (!official.IsPresent && !legacy.IsPresent)
        {
            return AuthenticateResult.NoResult();
        }

        if (!official.IsValid || !legacy.IsValid)
        {
            return AuthenticateResult.Fail("API key inválida.");
        }

        if (official.IsPresent &&
            legacy.IsPresent &&
            !string.Equals(official.Value, legacy.Value, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Headers de API key conflitantes.");
        }

        string apiKey = official.Value ?? legacy.Value!;
        ResolvedApiCredential? credential = await _credentialService.ResolveAsync(
            apiKey,
            Context.RequestAborted);

        if (credential is null)
        {
            return AuthenticateResult.Fail("API key inválida.");
        }

        Claim[] claims =
        [
            new(OrizonClaimTypes.TenantId, credential.TenantId.ToString()),
            new(OrizonClaimTypes.CredentialId, credential.Id.ToString()),
            new(OrizonClaimTypes.AgentId, credential.AgentId.ToString())
        ];

        var identity = new ClaimsIdentity(
            claims,
            AgentApiKeyDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(
            principal,
            AgentApiKeyDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }

    private HeaderValue ReadHeader(string headerName)
    {
        if (!Request.Headers.TryGetValue(
                headerName,
                out StringValues values))
        {
            return HeaderValue.Missing;
        }

        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            return HeaderValue.Invalid;
        }

        return new HeaderValue(
            IsPresent: true,
            IsValid: true,
            values[0]!.Trim());
    }

    private sealed record HeaderValue(
        bool IsPresent,
        bool IsValid,
        string? Value)
    {
        public static HeaderValue Missing { get; } = new(false, true, null);

        public static HeaderValue Invalid { get; } = new(true, false, null);
    }
}
