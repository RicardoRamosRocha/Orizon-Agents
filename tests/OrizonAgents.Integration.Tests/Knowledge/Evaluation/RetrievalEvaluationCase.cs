namespace OrizonAgents.Integration.Tests.Knowledge.Evaluation;

public sealed record RetrievalEvaluationCase
{
    public RetrievalEvaluationCase(string query, IEnumerable<Guid> expectedChunkIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(expectedChunkIds);

        Query = query;
        ExpectedChunkIds = expectedChunkIds.ToHashSet();
    }

    public string Query { get; }

    public IReadOnlySet<Guid> ExpectedChunkIds { get; }
}
