using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Knowledge.Embeddings;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Knowledge.Embeddings;

public sealed class OpenAiEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly IAiProviderCredentialService _credentialService;
    private readonly ILogger<OpenAiEmbeddingGenerator> _logger;

    public OpenAiEmbeddingGenerator(
        HttpClient httpClient,
        IAiProviderCredentialService credentialService,
        IConfiguration configuration,
        ILogger<OpenAiEmbeddingGenerator> logger)
    {
        _httpClient = httpClient;
        _credentialService = credentialService;
        _logger = logger;
        Model = configuration["Knowledge:Embeddings:OpenAI:Model"]
            ?? "text-embedding-3-small";
    }

    public string Provider => AiProvider.OpenAI.ToString();

    public string Model { get; }

    public int Dimensions => 1536;

    public async Task<float[]> GenerateAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required.", nameof(text));

        string? apiKey = await _credentialService.ResolveAsync(
            AiProvider.OpenAI,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Nenhuma credencial da OpenAI está configurada para este tenant.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/embeddings");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model = Model,
            input = text
        });

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OpenAI embedding request failed. HttpStatusCode: {HttpStatusCode}; Model: {Model}.",
                (int)response.StatusCode,
                Model);

            throw new InvalidOperationException(
                $"A OpenAI retornou {(int)response.StatusCode} ao gerar embedding.");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        JsonElement data = document.RootElement.GetProperty("data");

        if (data.GetArrayLength() == 0)
            throw new InvalidOperationException("A OpenAI não retornou embedding.");

        float[] embedding = data[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(value => value.GetSingle())
            .ToArray();

        if (embedding.Length != Dimensions)
        {
            throw new InvalidOperationException(
                $"O embedding retornado possui {embedding.Length} dimensões; esperado: {Dimensions}.");
        }

        return embedding;
    }
}
