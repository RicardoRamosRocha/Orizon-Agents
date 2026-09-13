using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge.Embeddings;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Agents.Credentials;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Agents.Credentials;
using OrizonAgents.Infrastructure.Knowledge.Embeddings;
using OrizonAgents.Infrastructure.Knowledge.Retrieval;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using Pgvector.EntityFrameworkCore;

namespace OrizonAgents.Integration.Tests.Knowledge;

public sealed class RealRagSmokeTests
{
    [RealRagFact]
    public async Task RetrieveAsync_WithPostgresPgvectorAndOpenAiEmbedding_ReturnsRelatedChunk()
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "OPENAI_API_KEY must be configured when the real RAG smoke test is enabled.");

        var currentTenant = new CurrentTenant();
        Tenant tenant = Tenant.Create(
            "Real RAG Smoke Tenant",
            $"real-rag-smoke-{Guid.NewGuid():N}");
        currentTenant.SetTenantId(tenant.Id);

        DbContextOptions<OrizonAgentsDbContext> options =
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseNpgsql(
                    TestDatabaseConnection.For("orizon_agents_tests"),
                    npgsql => npgsql.UseVector())
                .Options;

        await using var db = new OrizonAgentsDbContext(options, currentTenant);
        await db.Database.MigrateAsync();

        var agent = new AiAgent(
            tenant.Id,
            "Real RAG Smoke Agent",
            "Use the knowledge base.",
            AiProvider.OpenAI,
            "test-model");
        var knowledgeBase = new KnowledgeBase(
            tenant.Id,
            "Real RAG Smoke Knowledge Base");
        var document = new KnowledgeDocument(
            tenant.Id,
            knowledgeBase.Id,
            "real-rag-smoke.txt",
            "text/plain",
            256,
            $"real-rag-smoke/{Guid.NewGuid():N}.txt");
        document.MarkReady();
        var binding = new AgentKnowledgeBinding(
            tenant.Id,
            agent.Id,
            knowledgeBase.Id);

        db.AddRange(tenant, agent, knowledgeBase, document, binding);
        await db.SaveChangesAsync();

        var credentialProtector =
            new DataProtectionAiProviderCredentialProtector(
                new EphemeralDataProtectionProvider());
        var credentialService = new AiProviderCredentialService(
            db,
            currentTenant,
            credentialProtector);
        await credentialService.SaveAsync(AiProvider.OpenAI, apiKey);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();
        IEmbeddingGenerator embeddingGenerator =
            new OpenAiEmbeddingGenerator(
                httpClient,
                credentialService,
                configuration,
                NullLogger<OpenAiEmbeddingGenerator>.Instance);

        var targetChunk = new KnowledgeChunk(
            tenant.Id,
            document.Id,
            0,
            "Supplier invoices are archived in the secure finance records vault.");
        var distractorChunk = new KnowledgeChunk(
            tenant.Id,
            document.Id,
            1,
            "The office kitchen inventory contains coffee, tea, and paper cups.");
        db.KnowledgeChunks.AddRange(targetChunk, distractorChunk);
        await db.SaveChangesAsync();

        foreach (KnowledgeChunk chunk in new[] { targetChunk, distractorChunk })
        {
            float[] embedding = await embeddingGenerator.GenerateAsync(chunk.Content);
            db.KnowledgeChunkEmbeddings.Add(
                new KnowledgeChunkEmbedding(
                    tenant.Id,
                    chunk.Id,
                    embeddingGenerator.Provider,
                    embeddingGenerator.Model,
                    embedding));
        }

        await db.SaveChangesAsync();

        var semanticRetriever = new SemanticKnowledgeRetriever(
            db,
            currentTenant,
            embeddingGenerator);
        var hybridRetriever = new HybridKnowledgeRetriever(
            new KnowledgeRetriever(db),
            semanticRetriever,
            NullLogger<HybridKnowledgeRetriever>.Instance);

        IReadOnlyList<KnowledgeRetrievalResult> results =
            await hybridRetriever.RetrieveAsync(
                agent.Id,
                "Where are supplier invoices stored?",
                5);

        Assert.Contains(
            results,
            result =>
                result.KnowledgeChunkId == targetChunk.Id &&
                result.Content == targetChunk.Content);
    }

    private sealed class RealRagFactAttribute : FactAttribute
    {
        public RealRagFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("ORIZON_RUN_REAL_RAG_SMOKE"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                Skip =
                    "Set ORIZON_RUN_REAL_RAG_SMOKE=true to run the real RAG smoke test.";
            }
        }
    }
}
