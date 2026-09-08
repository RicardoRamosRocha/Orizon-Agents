using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Credentials;

public sealed class AiProviderApiKeyResolver : IAiProviderApiKeyResolver
{
    private const string AllowGlobalCredentialFallbackKey =
        "AiProviders:AllowGlobalCredentialFallback";

    private readonly IAiProviderCredentialService _credentialService;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment? _environment;

    public AiProviderApiKeyResolver(
        IAiProviderCredentialService credentialService,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        _credentialService = credentialService;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<string?> ResolveAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        string? tenantApiKey = await _credentialService.ResolveAsync(
            provider,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(tenantApiKey) ||
            !CanUseGlobalFallback(provider))
        {
            return tenantApiKey;
        }

        return provider switch
        {
            AiProvider.GoogleGemini => _configuration["GEMINI_API_KEY"],
            AiProvider.Groq => _configuration["GROQ_API_KEY"],
            _ => null
        };
    }

    private bool CanUseGlobalFallback(AiProvider provider) =>
        provider is AiProvider.GoogleGemini or AiProvider.Groq &&
        _environment?.IsDevelopment() == true &&
        _configuration.GetValue<bool>(AllowGlobalCredentialFallbackKey);
}
