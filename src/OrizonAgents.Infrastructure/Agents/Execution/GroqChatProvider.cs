using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Application.Agents.Execution.Models;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class GroqChatProvider : IAiChatProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IAiProviderCredentialService _credentialService;

    public GroqChatProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        IAiProviderCredentialService credentialService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _credentialService = credentialService;
    }

    public string ProviderName => "Groq";

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
            _configuration["GROQ_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? throw new InvalidOperationException(
                "A chave GROQ_API_KEY nÃ£o estÃ¡ configurada.");

        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = systemPrompt
            }
        };

        if (!string.IsNullOrWhiteSpace(operationalContext))
        {
            messages.Add(new
            {
                role = "system",
                content =
                    "Contexto operacional fornecido pela aplicaÃ§Ã£o consumidora para esta execuÃ§Ã£o:\n" +
                    operationalContext
            });
        }

        foreach (AiChatMessage message in history)
        {
            if (message.Role is not ("user" or "assistant"))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            messages.Add(new
            {
                role = message.Role,
                content = message.Content
            });
        }

        messages.Add(new
        {
            role = "user",
            content = userMessage
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "openai/v1/chat/completions");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        request.Content = JsonContent.Create(new
        {
            model,
            messages,
            temperature
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
                $"Groq retornou {(int)response.StatusCode} em {request.RequestUri}: {responseBody}");
        }

        using JsonDocument document =
            JsonDocument.Parse(responseBody);

        JsonElement choices =
            document.RootElement.GetProperty("choices");

        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                "A Groq nÃ£o retornou nenhuma resposta.");
        }

        string? content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "A Groq retornou uma resposta vazia.");
        }

        return content.Trim();
    }
}
