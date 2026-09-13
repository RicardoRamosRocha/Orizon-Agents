using OrizonAgents.Application.Knowledge.Retrieval.Models;

namespace OrizonAgents.Integration.Tests.Knowledge.Evaluation;

public static class RetrievalEvaluator
{
    public static RetrievalEvaluationResult Evaluate(
        IReadOnlyList<KnowledgeRetrievalResult> results,
        IEnumerable<Guid> expectedChunkIds,
        int k)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(expectedChunkIds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);

        IReadOnlySet<Guid> expectedIds = expectedChunkIds.ToHashSet();
        KnowledgeRetrievalResult[] topK = results
            .Where(result => result.KnowledgeChunkId.HasValue)
            .Take(k)
            .ToArray();

        int relevantFound = topK
            .Select(result => result.KnowledgeChunkId!.Value)
            .Where(expectedIds.Contains)
            .Distinct()
            .Count();

        int firstRelevantRank = Array.FindIndex(
            topK,
            result => result.KnowledgeChunkId.HasValue &&
                      expectedIds.Contains(result.KnowledgeChunkId.Value));

        return new RetrievalEvaluationResult(
            k,
            relevantFound > 0 ? 1 : 0,
            expectedIds.Count == 0 ? 0 : (double)relevantFound / expectedIds.Count,
            firstRelevantRank < 0 ? 0 : 1d / (firstRelevantRank + 1));
    }
}
