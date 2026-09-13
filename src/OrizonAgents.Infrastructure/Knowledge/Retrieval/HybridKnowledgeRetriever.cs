using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Knowledge.Retrieval;
using OrizonAgents.Application.Knowledge.Retrieval.Models;

namespace OrizonAgents.Infrastructure.Knowledge.Retrieval;

public sealed class HybridKnowledgeRetriever : IHybridKnowledgeRetriever
{
    private const double LexicalWeight = 0.3;
    private const double SemanticWeight = 0.7;
    private const int MaxSafeResults = 10;

    private readonly IKnowledgeRetriever _lexicalRetriever;
    private readonly ISemanticKnowledgeRetriever _semanticRetriever;
    private readonly ILogger<HybridKnowledgeRetriever> _logger;

    public HybridKnowledgeRetriever(
        IKnowledgeRetriever lexicalRetriever,
        ISemanticKnowledgeRetriever semanticRetriever,
        ILogger<HybridKnowledgeRetriever> logger)
    {
        _lexicalRetriever = lexicalRetriever;
        _semanticRetriever = semanticRetriever;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
        Guid agentId,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(query) ||
            maxResults <= 0)
        {
            return Array.Empty<KnowledgeRetrievalResult>();
        }

        int safeMaxResults = Math.Min(maxResults, MaxSafeResults);
        IReadOnlyList<KnowledgeRetrievalResult> lexicalResults =
            await _lexicalRetriever.RetrieveAsync(
                agentId,
                query,
                safeMaxResults,
                cancellationToken);

        IReadOnlyList<KnowledgeRetrievalResult> semanticResults;

        try
        {
            semanticResults = await _semanticRetriever.RetrieveAsync(
                agentId,
                query,
                safeMaxResults,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Semantic retrieval failed for AgentId {AgentId}; using lexical results only.",
                agentId);

            return lexicalResults
                .Take(safeMaxResults)
                .ToArray();
        }

        var candidates = new Dictionary<RetrievalKey, Candidate>();
        AddLexicalCandidates(candidates, lexicalResults);
        AddSemanticCandidates(candidates, semanticResults);

        double maxLexicalScore = candidates.Values
            .Where(candidate => candidate.LexicalScore.HasValue)
            .Select(candidate => candidate.LexicalScore!.Value)
            .DefaultIfEmpty(0)
            .Max();
        double maxSemanticScore = candidates.Values
            .Where(candidate => candidate.SemanticScore.HasValue)
            .Select(candidate => candidate.SemanticScore!.Value)
            .DefaultIfEmpty(0)
            .Max();

        return candidates.Values
            .Select(candidate => candidate.ToResult(
                Normalize(candidate.LexicalScore, maxLexicalScore),
                Normalize(candidate.SemanticScore, maxSemanticScore),
                LexicalWeight,
                SemanticWeight))
            .OrderByDescending(result => result.HybridScore)
            .ThenByDescending(result => result.SemanticScore ?? 0)
            .ThenByDescending(result => result.LexicalScore ?? 0)
            .ThenBy(result => result.DocumentName)
            .ThenBy(result => result.ChunkPosition)
            .Take(safeMaxResults)
            .ToArray();
    }

    private static void AddLexicalCandidates(
        IDictionary<RetrievalKey, Candidate> candidates,
        IReadOnlyList<KnowledgeRetrievalResult> results)
    {
        foreach (KnowledgeRetrievalResult result in results)
        {
            Candidate candidate = GetOrCreate(candidates, result);
            candidate.LexicalScore = result.LexicalScore;
        }
    }

    private static void AddSemanticCandidates(
        IDictionary<RetrievalKey, Candidate> candidates,
        IReadOnlyList<KnowledgeRetrievalResult> results)
    {
        foreach (KnowledgeRetrievalResult result in results)
        {
            Candidate candidate = GetOrCreate(candidates, result);
            candidate.SemanticScore = result.SemanticScore;
        }
    }

    private static Candidate GetOrCreate(
        IDictionary<RetrievalKey, Candidate> candidates,
        KnowledgeRetrievalResult result)
    {
        RetrievalKey key = RetrievalKey.From(result);

        if (!candidates.TryGetValue(key, out Candidate? candidate))
        {
            candidate = new Candidate(result);
            candidates.Add(key, candidate);
        }

        return candidate;
    }

    private static double Normalize(double? score, double maxScore)
    {
        if (!score.HasValue || maxScore <= 0)
        {
            return 0;
        }

        return Math.Clamp(score.Value / maxScore, 0, 1);
    }

    private readonly record struct RetrievalKey(
        Guid? KnowledgeChunkId,
        Guid DocumentId,
        int ChunkPosition)
    {
        public static RetrievalKey From(KnowledgeRetrievalResult result) =>
            result.KnowledgeChunkId is Guid chunkId && chunkId != Guid.Empty
                ? new(chunkId, Guid.Empty, 0)
                : new(null, result.DocumentId, result.ChunkPosition);
    }

    private sealed class Candidate
    {
        private readonly KnowledgeRetrievalResult _result;

        public Candidate(KnowledgeRetrievalResult result)
        {
            _result = result;
        }

        public double? LexicalScore { get; set; }

        public double? SemanticScore { get; set; }

        public KnowledgeRetrievalResult ToResult(
            double normalizedLexical,
            double normalizedSemantic,
            double lexicalWeight,
            double semanticWeight) =>
            _result with
            {
                LexicalScore = normalizedLexical,
                SemanticScore = normalizedSemantic,
                HybridScore =
                    normalizedLexical * lexicalWeight +
                    normalizedSemantic * semanticWeight
            };
    }
}
