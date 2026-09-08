using System.Net.Http.Headers;
using System.Text.Json;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class GroqModelCatalog : IAiProviderSpecificModelCatalog
{
    private readonly HttpClient _httpClient;
    private readonly IAiProviderApiKeyResolver _apiKeyResolver;

    public GroqModelCatalog(
        HttpClient httpClient,
        IAiProviderApiKeyResolver apiKeyResolver)
    {
        _httpClient = httpClient;
        _apiKeyResolver = apiKeyResolver;
    }

    public AiProvider Provider => AiProvider.Groq;

    public string DisplayName => "Groq";

    public async Task<IReadOnlyList<AiProviderModel>> ListAsync(
        CancellationToken cancellationToken = default)
    {

        string? apiKey =
            await _apiKeyResolver.ResolveAsync(
                AiProvider.Groq,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Nenhuma credencial da Groq está configurada para este tenant.");
        }

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "openai/v1/models");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                apiKey);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        string responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Groq retornou {(int)response.StatusCode} ao consultar modelos.");
        }

        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty(
                "data",
                out JsonElement models))
        {
            return Array.Empty<AiProviderModel>();
        }

        return models
            .EnumerateArray()
            .Select(model =>
                model.TryGetProperty(
                    "id",
                    out JsonElement idElement)
                    ? idElement.GetString()
                    : null)
            .Where(id =>
                !string.IsNullOrWhiteSpace(id))
            .Select(id =>
                new AiProviderModel(
                    id!,
                    id!))
            .OrderBy(model => model.DisplayName)
            .ToArray();
    }
}
