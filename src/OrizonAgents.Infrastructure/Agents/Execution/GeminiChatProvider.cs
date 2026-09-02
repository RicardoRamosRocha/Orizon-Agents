using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class GeminiChatProvider : IAiChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IAiProviderCredentialService _credentialService;

    public GeminiChatProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        IAiProviderCredentialService credentialService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _credentialService = credentialService;
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

        string effectiveSystemPrompt = systemPrompt;

        if (!string.IsNullOrWhiteSpace(operationalContext))
        {
            effectiveSystemPrompt +=
                "\n\nContexto operacional fornecido pela aplicaÃƒÂ§ÃƒÂ£o consumidora " +
                "para esta execuÃƒÂ§ÃƒÂ£o:\n" +
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

        const int maxAttempts = 3;
        string? responseBody = null;
        HttpStatusCode responseStatusCode = default;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
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

            try
            {
                using HttpResponseMessage response =
                    await _httpClient.SendAsync(
                        request,
                        cancellationToken);

                responseBody =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                responseStatusCode = response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                bool isTransient =
                    response.StatusCode is
                        HttpStatusCode.TooManyRequests or
                        HttpStatusCode.BadGateway or
                        HttpStatusCode.ServiceUnavailable or
                        HttpStatusCode.GatewayTimeout;

                if (!isTransient || attempt == maxAttempts)
                {
                    throw new InvalidOperationException(
                        $"Gemini retornou {(int)response.StatusCode}: {responseBody}");
                }
            }
            catch (TaskCanceledException) when (
                !cancellationToken.IsCancellationRequested)
            {
                if (attempt == maxAttempts)
                {
                    throw new InvalidOperationException(
                        "O Gemini não respondeu dentro do tempo limite após múltiplas tentativas.");
                }
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(500 * attempt),
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new InvalidOperationException(
                $"Gemini retornou {(int)responseStatusCode} sem conteúdo.");
        }

        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("candidates", out JsonElement candidates) ||
            candidates.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "O Gemini nÃƒÂ£o retornou nenhuma resposta.");
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
