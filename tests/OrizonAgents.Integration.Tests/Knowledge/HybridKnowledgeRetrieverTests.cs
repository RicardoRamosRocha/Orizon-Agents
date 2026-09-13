using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Knowledge.Retrieval;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Infrastructure.Knowledge.Retrieval;

namespace OrizonAgents.Integration.Tests.Knowledge;

public sealed class HybridKnowledgeRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_NormalizesWeightsAndDeduplicatesChunks()
    {
        Guid agentId = Guid.NewGuid();
        Guid sharedChunkId = Guid.NewGuid();

        var retriever = new HybridKnowledgeRetriever(
            new StubLexicalRetriever(
                Result(sharedChunkId, "shared", lexicalScore: 8),
                Result(Guid.NewGuid(), "lexical", lexicalScore: 4)),
            new StubSemanticRetriever(
                Result(sharedChunkId, "shared", semanticScore: 0.9),
                Result(Guid.NewGuid(), "semantic", semanticScore: 0.8)),
            NullLogger<HybridKnowledgeRetriever>.Instance);

        IReadOnlyList<KnowledgeRetrievalResult> results =
            await retriever.RetrieveAsync(agentId, "query", 10);

        Assert.Equal(3, results.Count);
        Assert.Equal("shared", results[0].Content);
        Assert.Equal(1, results[0].LexicalScore);
        Assert.Equal(1, results[0].SemanticScore);
        Assert.Equal(1, results[0].HybridScore);
        Assert.Contains(results, result => result.Content == "lexical");
        Assert.Contains(results, result => result.Content == "semantic");
    }

    [Fact]
    public async Task RetrieveAsync_FallsBackToLexicalWhenSemanticFails()
    {
        Guid agentId = Guid.NewGuid();
        var retriever = new HybridKnowledgeRetriever(
            new StubLexicalRetriever(
                Result(Guid.NewGuid(), "lexical", lexicalScore: 3)),
            new FailingSemanticRetriever(),
            NullLogger<HybridKnowledgeRetriever>.Instance);

        IReadOnlyList<KnowledgeRetrievalResult> results =
            await retriever.RetrieveAsync(agentId, "query", 5);

        Assert.Single(results);
        Assert.Equal("lexical", results[0].Content);
        Assert.Equal(3, results[0].LexicalScore);
        Assert.Null(results[0].HybridScore);
    }

    [Fact]
    public async Task RetrieveAsync_UsesLexicalResultsWhenSemanticReturnsEmpty()
    {
        var retriever = new HybridKnowledgeRetriever(
            new StubLexicalRetriever(
                Result(Guid.NewGuid(), "lexical", lexicalScore: 5)),
            new StubSemanticRetriever(),
            NullLogger<HybridKnowledgeRetriever>.Instance);

        IReadOnlyList<KnowledgeRetrievalResult> results =
            await retriever.RetrieveAsync(Guid.NewGuid(), "query");

        Assert.Single(results);
        Assert.Equal("lexical", results[0].Content);
        Assert.Equal(1, results[0].LexicalScore);
        Assert.Equal(0, results[0].SemanticScore);
        Assert.Equal(0.3, results[0].HybridScore);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsEmptyWhenBothStrategiesReturnEmpty()
    {
        var retriever = new HybridKnowledgeRetriever(
            new StubLexicalRetriever(),
            new StubSemanticRetriever(),
            NullLogger<HybridKnowledgeRetriever>.Instance);

        IReadOnlyList<KnowledgeRetrievalResult> results =
            await retriever.RetrieveAsync(Guid.NewGuid(), "unrelated query");

        Assert.Empty(results);
    }

    [Fact]
    public async Task RetrieveAsync_EnforcesMaximumOfTenResults()
    {
        KnowledgeRetrievalResult[] candidates = Enumerable.Range(0, 20)
            .Select(index => Result(Guid.NewGuid(), $"chunk-{index}", lexicalScore: 20 - index))
            .ToArray();

        var retriever = new HybridKnowledgeRetriever(
            new StubLexicalRetriever(candidates),
            new StubSemanticRetriever(),
            NullLogger<HybridKnowledgeRetriever>.Instance);

        IReadOnlyList<KnowledgeRetrievalResult> results =
            await retriever.RetrieveAsync(Guid.NewGuid(), "query", 100);

        Assert.Equal(10, results.Count);
    }

    private static KnowledgeRetrievalResult Result(
        Guid chunkId,
        string content,
        double? lexicalScore = null,
        double? semanticScore = null) =>
        new(
            Guid.NewGuid(),
            "Base",
            Guid.NewGuid(),
            "document.txt",
            0,
            content,
            semanticScore,
            lexicalScore,
            null,
            chunkId);

    private sealed class StubLexicalRetriever(params KnowledgeRetrievalResult[] results) : IKnowledgeRetriever
    {
        public Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
            Guid agentId,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRetrievalResult>>(
                results.Take(maxResults).ToArray());
    }

    private sealed class StubSemanticRetriever(params KnowledgeRetrievalResult[] results) : ISemanticKnowledgeRetriever
    {
        public Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
            Guid agentId,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRetrievalResult>>(
                results.Take(maxResults).ToArray());
    }

    private sealed class FailingSemanticRetriever : ISemanticKnowledgeRetriever
    {
        public Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
            Guid agentId,
            string query,
            int maxResults = 5,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("semantic unavailable");
    }
}
