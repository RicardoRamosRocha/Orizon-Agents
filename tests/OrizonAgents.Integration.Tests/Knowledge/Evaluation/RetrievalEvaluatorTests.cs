using OrizonAgents.Application.Knowledge.Retrieval.Models;

namespace OrizonAgents.Integration.Tests.Knowledge.Evaluation;

public sealed class RetrievalEvaluatorTests
{
    [Fact]
    public void Evaluate_CalculatesHitRecallAndMrrUsingOnlyTopKChunkIds()
    {
        Guid expectedFirst = Guid.NewGuid();
        Guid expectedSecond = Guid.NewGuid();

        RetrievalEvaluationResult evaluation = RetrievalEvaluator.Evaluate(
            new[]
            {
                Result(null),
                Result(Guid.NewGuid()),
                Result(Guid.NewGuid()),
                Result(expectedFirst),
                Result(expectedSecond)
            },
            new[] { expectedFirst, expectedSecond },
            2);

        Assert.Equal(2, evaluation.K);
        Assert.Equal(0, evaluation.HitAtK);
        Assert.Equal(0, evaluation.RecallAtK);
        Assert.Equal(0, evaluation.MRR);
    }

    [Fact]
    public void Evaluate_UsesRankAfterIgnoringResultsWithoutChunkIds()
    {
        Guid expectedFirst = Guid.NewGuid();
        Guid expectedSecond = Guid.NewGuid();

        RetrievalEvaluationResult evaluation = RetrievalEvaluator.Evaluate(
            new[]
            {
                Result(null),
                Result(expectedFirst),
                Result(expectedFirst),
                Result(expectedSecond)
            },
            new[] { expectedFirst, expectedSecond },
            3);

        Assert.Equal(1, evaluation.HitAtK);
        Assert.Equal(1, evaluation.RecallAtK);
        Assert.Equal(1, evaluation.MRR);
    }

    [Fact]
    public void Evaluate_ReturnsZeroMetricsWhenThereAreNoExpectedChunks()
    {
        RetrievalEvaluationResult evaluation = RetrievalEvaluator.Evaluate(
            new[] { Result(Guid.NewGuid()) },
            Array.Empty<Guid>(),
            5);

        Assert.Equal(0, evaluation.HitAtK);
        Assert.Equal(0, evaluation.RecallAtK);
        Assert.Equal(0, evaluation.MRR);
    }

    [Fact]
    public void EvaluationCase_CopiesExpectedChunkIdsAndRetainsQuery()
    {
        Guid expectedChunkId = Guid.NewGuid();
        var source = new[] { expectedChunkId, expectedChunkId };

        var evaluationCase = new RetrievalEvaluationCase("Como configurar o agente?", source);

        Assert.Equal("Como configurar o agente?", evaluationCase.Query);
        Assert.Single(evaluationCase.ExpectedChunkIds);
        Assert.Contains(expectedChunkId, evaluationCase.ExpectedChunkIds);
    }

    [Fact]
    public void Evaluate_RejectsNonPositiveK()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RetrievalEvaluator.Evaluate(Array.Empty<KnowledgeRetrievalResult>(), Array.Empty<Guid>(), 0));
    }

    private static KnowledgeRetrievalResult Result(Guid? chunkId) =>
        new(
            Guid.NewGuid(),
            "Base",
            Guid.NewGuid(),
            "document.txt",
            0,
            "content",
            KnowledgeChunkId: chunkId);
}
