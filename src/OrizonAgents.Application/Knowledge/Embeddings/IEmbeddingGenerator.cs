namespace OrizonAgents.Application.Knowledge.Embeddings;

public interface IEmbeddingGenerator
{
    string Provider { get; }

    string Model { get; }

    int Dimensions { get; }

    Task<float[]> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default);
}
