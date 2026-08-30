using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class GeminiModelCatalog : IAiProviderSpecificModelCatalog
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IAiProviderCredentialService _credentialService;

    public GeminiModelCatalog(
        HttpClient httpClient,
        IConfiguration configuration,
        IAiProviderCredentialService credentialService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _credentialService = credentialService;
    }

    public AiProvider Provider => AiProvider.GoogleGemini;

    public async Task<IReadOnlyList<AiProviderModel>> ListAsync(
        CancellationToken cancellationToken = default)
    {

        string? apiKey =
            await _credentialService.ResolveAsync(
                AiProvider.GoogleGemini,
                cancellationToken);

        apiKey ??=
            _configuration["GEMINI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Nenhuma credencial do Google Gemini está configurada para este tenant.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "v1beta/models");

        request.Headers.Add("x-goog-api-key", apiKey);

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
                $"Gemini retornou {(int)response.StatusCode} ao consultar modelos: {responseBody}");
        }

        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        if (!document.RootElement.TryGetProperty(
                "models",
                out JsonElement models))
        {
            return Array.Empty<AiProviderModel>();
        }

        var result = new List<AiProviderModel>();

        foreach (JsonElement model in models.EnumerateArray())
        {
            if (!SupportsGenerateContent(model))
            {
                continue;
            }

            string? name =
                model.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string id = name.StartsWith(
                "models/",
                StringComparison.OrdinalIgnoreCase)
                    ? name["models/".Length..]
                    : name;

            string displayName =
                model.TryGetProperty(
                    "displayName",
                    out JsonElement displayNameElement)
                    ? displayNameElement.GetString() ?? id
                    : id;

            result.Add(
                new AiProviderModel(
                    id,
                    displayName));
        }

        return result
            .OrderBy(model => model.DisplayName)
            .ToArray();
    }

    private static bool SupportsGenerateContent(JsonElement model)
    {
        if (!model.TryGetProperty(
                "supportedGenerationMethods",
                out JsonElement methods))
        {
            return false;
        }

        return methods
            .EnumerateArray()
            .Any(method =>
                string.Equals(
                    method.GetString(),
                    "generateContent",
                    StringComparison.OrdinalIgnoreCase));
    }
}
