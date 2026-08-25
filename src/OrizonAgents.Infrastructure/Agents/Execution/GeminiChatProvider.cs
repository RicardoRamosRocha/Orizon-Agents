using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class GeminiChatProvider : IAiChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiChatProvider(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public string ProviderName => "GoogleGemini";

    public async Task<string> CompleteAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        string? operationalContext = null,
        CancellationToken cancellationToken = default)
    {
        string apiKey =
            _configuration["GEMINI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException(
                "A chave GEMINI_API_KEY não está configurada.");

        string effectiveSystemPrompt = systemPrompt;

        if (!string.IsNullOrWhiteSpace(operationalContext))
        {
            effectiveSystemPrompt +=
                "\n\nContexto operacional fornecido pela aplicação consumidora " +
                "para esta execução:\n" +
                operationalContext;
        }

        var contents = new List<object>();

        foreach (AiChatMessage message in history)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            string? role = message.Role switch
            {
                "user" => "user",
                "assistant" => "model",
                _ => null
            };

            if (role is null)
            {
                continue;
            }

            contents.Add(new
            {
                role,
                parts = new[]
                {
                    new { text = message.Content }
                }
            });
        }

        contents.Add(new
        {
            role = "user",
            parts = new[]
            {
                new { text = userMessage }
            }
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{model}:generateContent");

        request.Headers.Add("x-goog-api-key", apiKey);

        request.Content = JsonContent.Create(new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = effectiveSystemPrompt }
                }
            },
            contents,
            generationConfig = new
            {
                temperature
            }
        });

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        string responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini retornou {(int)response.StatusCode}: {responseBody}");
        }

        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("candidates", out JsonElement candidates) ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "O Gemini não retornou nenhuma resposta.");
        }

        JsonElement parts = candidates[0]
            .GetProperty("content")
            .GetProperty("parts");

        string content = string.Join(
            "\n",
            parts.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "O Gemini retornou uma resposta vazia.");
        }

        return content.Trim();
    }
}
