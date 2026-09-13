using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class AiProviderModelCatalogTests
{
    [Fact]
    public async Task RegisteredCatalogs_AreTheSourceOfSupportedProvidersAndRouting()
    {
        var openAi = new StubCatalog(
            AiProvider.OpenAI,
            "OpenAI",
            [new AiProviderModel("gpt-test", "GPT Test")]);
        var catalog = new AiProviderModelCatalog(
        [
            new StubCatalog(AiProvider.Groq, "Groq", []),
            openAi,
            new StubCatalog(AiProvider.GoogleGemini, "Google Gemini", [])
        ]);

        Assert.Equal(
        [
            new AiProviderDescriptor(AiProvider.OpenAI, "OpenAI"),
            new AiProviderDescriptor(AiProvider.GoogleGemini, "Google Gemini"),
            new AiProviderDescriptor(AiProvider.Groq, "Groq")
        ],
            catalog.Providers);
        Assert.DoesNotContain(
            catalog.Providers,
            item => item.Provider == AiProvider.AnthropicClaude);

        IReadOnlyList<AiProviderModel> models =
            await catalog.ListAsync(AiProvider.OpenAI);

        Assert.Equal("gpt-test", Assert.Single(models).Id);
        Assert.Equal(1, openAi.ListCalls);
        Assert.True(await catalog.IsValidAsync(
            AiProvider.OpenAI,
            "GPT-TEST"));
    }

    private sealed class StubCatalog(
        AiProvider provider,
        string displayName,
        IReadOnlyList<AiProviderModel> models)
        : IAiProviderSpecificModelCatalog
    {
        public AiProvider Provider => provider;
        public string DisplayName => displayName;
        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<AiProviderModel>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(models);
        }
    }
}
