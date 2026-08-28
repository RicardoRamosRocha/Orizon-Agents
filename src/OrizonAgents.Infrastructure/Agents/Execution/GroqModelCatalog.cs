using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class GroqModelCatalog : IAiProviderModelCatalog
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IAiProviderCredentialService _credentialService;

    public GroqModelCatalog(
        HttpClient httpClient,
        IConfiguration configuration,
        IAiProviderCredentialService credentialService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _credentialService = credentialService;
    }

    public async Task<IReadOnlyList<AiProviderModel>> ListAsync(
        AiProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (provider != AiProvider.Groq)
        {
            return Array.Empty<AiProviderModel>();
        }

        string? apiKey =
            await _credentialService.ResolveAsync(
                AiProvider.Groq,
                cancellationToken);

        apiKey ??=
            _configuration["GROQ_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");

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
                $"Groq retornou {(int)response.StatusCode} ao consultar modelos: {responseBody}");
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

    public async Task<bool> IsValidAsync(
        AiProvider provider,
        string model,
        CancellationToken cancellationToken = default)
    {
        if (provider != AiProvider.Groq ||
            string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        IReadOnlyList<AiProviderModel> models =
            await ListAsync(
                provider,
                cancellationToken);

        return models.Any(item =>
            string.Equals(
                item.Id,
                model.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }
}
