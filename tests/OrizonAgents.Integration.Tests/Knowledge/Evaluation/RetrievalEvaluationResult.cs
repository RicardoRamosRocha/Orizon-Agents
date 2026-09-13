namespace OrizonAgents.Integration.Tests.Knowledge.Evaluation;

public sealed record RetrievalEvaluationResult(
    int K,
    int HitAtK,
    double RecallAtK,
    double MRR);
