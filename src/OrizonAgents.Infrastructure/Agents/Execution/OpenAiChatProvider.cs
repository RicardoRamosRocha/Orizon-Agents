using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class OpenAiChatProvider(
    HttpClient httpClient,
    IAiProviderCredentialService credentialService)
    : IAiChatProvider
{
    public string ProviderName => AiProvider.OpenAI.ToString();

    public async Task<AiChatCompletionResult> CompleteAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        string? operationalContext = null,
        CancellationToken cancellationToken = default)
    {
        string apiKey = await ResolveApiKeyAsync(cancellationToken);
        string instructions = BuildInstructions(
            systemPrompt,
            operationalContext);

        var input = new List<object>();
        foreach (AiChatMessage message in history)
        {
            if (message.Role is not ("user" or "assistant") ||
                string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            input.Add(new
            {
                role = message.Role,
                content = message.Content
            });
        }

        input.Add(new
        {
            role = "user",
            content = userMessage
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/responses");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            model,
            instructions,
            input,
            temperature,
            store = false
        });

        using HttpResponseMessage response =
            await httpClient.SendAsync(request, cancellationToken);
        string responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI retornou {(int)response.StatusCode}.");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        string content = ReadOutputText(document.RootElement);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "A OpenAI retornou uma resposta vazia.");
        }

        return new AiChatCompletionResult(
            content.Trim(),
            ReadUsage(document.RootElement));
    }

    private async Task<string> ResolveApiKeyAsync(
        CancellationToken cancellationToken)
    {
        string? apiKey = await credentialService.ResolveAsync(
            AiProvider.OpenAI,
            cancellationToken);

        return !string.IsNullOrWhiteSpace(apiKey)
            ? apiKey
            : throw new InvalidOperationException(
                "Nenhuma credencial da OpenAI está configurada para este tenant.");
    }

    private static string BuildInstructions(
        string systemPrompt,
        string? operationalContext)
    {
        if (string.IsNullOrWhiteSpace(operationalContext))
        {
            return systemPrompt;
        }

        return systemPrompt +
            "\n\nContexto operacional fornecido pela aplicação consumidora " +
            "para esta execução:\n" +
            operationalContext;
    }

    private static string ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out JsonElement output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var texts = new List<string>();
        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out JsonElement contents) ||
                contents.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement content in contents.EnumerateArray())
            {
                if (content.TryGetProperty("type", out JsonElement type) &&
                    string.Equals(
                        type.GetString(),
                        "output_text",
                        StringComparison.Ordinal) &&
                    content.TryGetProperty("text", out JsonElement text) &&
                    !string.IsNullOrWhiteSpace(text.GetString()))
                {
                    texts.Add(text.GetString()!);
                }
            }
        }

        return string.Join("\n", texts);
    }

    private static AiChatUsage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        long? input = ReadInt64(usage, "input_tokens");
        long? output = ReadInt64(usage, "output_tokens");
        long? total = ReadInt64(usage, "total_tokens");

        return input.HasValue || output.HasValue || total.HasValue
            ? new AiChatUsage(input, output, total)
            : null;
    }

    private static long? ReadInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) &&
        value.TryGetInt64(out long result)
            ? result
            : null;
}
