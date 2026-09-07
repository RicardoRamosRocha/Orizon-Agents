using System.Net.Http.Headers;
using System.Text.Json;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class OpenAiModelCatalog(
    HttpClient httpClient,
    IAiProviderCredentialService credentialService)
    : IAiProviderSpecificModelCatalog
{
    public AiProvider Provider => AiProvider.OpenAI;

    public string DisplayName => "OpenAI";

    public async Task<IReadOnlyList<AiProviderModel>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        string? apiKey = await credentialService.ResolveAsync(
            AiProvider.OpenAI,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Nenhuma credencial da OpenAI está configurada para este tenant.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "v1/models");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        using HttpResponseMessage response =
            await httpClient.SendAsync(request, cancellationToken);
        string responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI retornou {(int)response.StatusCode} ao consultar modelos.");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty(
                "data",
                out JsonElement models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AiProviderModel>();
        }

        return models
            .EnumerateArray()
            .Select(model =>
                model.TryGetProperty("id", out JsonElement id)
                    ? id.GetString()
                    : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => new AiProviderModel(id!, id!))
            .OrderBy(model => model.DisplayName)
            .ToArray();
    }
}
