using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Credentials;

namespace OrizonAgents.Integration.Tests.Agents.Credentials;

public sealed class AiProviderApiKeyResolverTests
{
    [Theory]
    [InlineData(AiProvider.GoogleGemini, "GEMINI_API_KEY")]
    [InlineData(AiProvider.Groq, "GROQ_API_KEY")]
    public async Task ResolveAsync_DevelopmentWithExplicitOptIn_AllowsGlobalFallback(
        AiProvider provider,
        string configurationKey)
    {
        var resolver = CreateResolver(
            null, "Development", true, configurationKey, "global-key");

        Assert.Equal("global-key", await resolver.ResolveAsync(provider));
    }

    [Theory]
    [InlineData(AiProvider.GoogleGemini, "GEMINI_API_KEY")]
    [InlineData(AiProvider.Groq, "GROQ_API_KEY")]
    public async Task ResolveAsync_DevelopmentWithoutOptIn_BlocksGlobalFallback(
        AiProvider provider,
        string configurationKey)
    {
        var resolver = CreateResolver(
            null, "Development", false, configurationKey, "global-key");

        Assert.Null(await resolver.ResolveAsync(provider));
    }

    [Theory]
    [InlineData(AiProvider.GoogleGemini, "GEMINI_API_KEY")]
    [InlineData(AiProvider.Groq, "GROQ_API_KEY")]
    public async Task ResolveAsync_Production_BlocksGlobalFallbackEvenWhenOptedIn(
        AiProvider provider,
        string configurationKey)
    {
        var resolver = CreateResolver(
            null, "Production", true, configurationKey, "global-key");

        Assert.Null(await resolver.ResolveAsync(provider));
    }

    [Fact]
    public async Task ResolveAsync_OpenAiNeverUsesGlobalFallback()
    {
        var resolver = CreateResolver(
            null, "Development", true, "OPENAI_API_KEY", "global-key");

        Assert.Null(await resolver.ResolveAsync(AiProvider.OpenAI));
    }

    [Fact]
    public async Task ResolveAsync_PrefersTenantCredential()
    {
        var resolver = CreateResolver(
            "tenant-key", "Production", false, "GROQ_API_KEY", "global-key");

        Assert.Equal("tenant-key", await resolver.ResolveAsync(AiProvider.Groq));
    }

    private static AiProviderApiKeyResolver CreateResolver(
        string? tenantKey,
        string environmentName,
        bool allowFallback,
        string globalKeyName,
        string globalKey)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiProviders:AllowGlobalCredentialFallback"] = allowFallback.ToString(),
                [globalKeyName] = globalKey
            })
            .Build();
        return new AiProviderApiKeyResolver(
            new StubCredentialService(tenantKey),
            configuration,
            new TestHostEnvironment(environmentName));
    }

    private sealed class StubCredentialService(string? apiKey)
        : IAiProviderCredentialService
    {
        public Task<string?> ResolveAsync(AiProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(apiKey);
        public Task SaveAsync(AiProvider provider, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasCredentialAsync(AiProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveAsync(AiProvider provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
