using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Agents.Execution;

public sealed class OpenAiChatProvider(
    HttpClient httpClient,
    IAiProviderCredentialService credentialService,
    ILogger<OpenAiChatProvider> logger)
    : IAiChatProvider
{
    public string ProviderName => AiProvider.OpenAI.ToString();

    public AiChatToolInvocationMode ToolInvocationMode =>
        AiChatToolInvocationMode.Structured;

    public Task<AiChatCompletionResult> CompleteAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        string? operationalContext = null,
        CancellationToken cancellationToken = default)
    {
        return CompleteCoreAsync(
            model,
            systemPrompt,
            userMessage,
            history,
            temperature,
            [],
            operationalContext,
            null,
            [],
            cancellationToken);
    }

    public Task<AiChatCompletionResult> CompleteWithToolsAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        IReadOnlyList<AgentToolDefinition> tools,
        string? operationalContext = null,
        CancellationToken cancellationToken = default)
    {
        return CompleteCoreAsync(
            model,
            systemPrompt,
            userMessage,
            history,
            temperature,
            tools,
            operationalContext,
            null,
            [],
            cancellationToken);
    }

    public Task<AiChatCompletionResult> ContinueWithToolsAsync(
        string model,
        string systemPrompt,
        double temperature,
        IReadOnlyList<AgentToolDefinition> tools,
        string continuationToken,
        IReadOnlyList<AgentToolResult> toolResults,
        string? operationalContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(continuationToken);
        ArgumentNullException.ThrowIfNull(toolResults);

        return CompleteCoreAsync(
            model, systemPrompt, string.Empty, [], temperature, tools,
            operationalContext, continuationToken, toolResults, cancellationToken);
    }
    private async Task<AiChatCompletionResult> CompleteCoreAsync(
        string model,
        string systemPrompt,
        string userMessage,
        IReadOnlyList<AiChatMessage> history,
        double temperature,
        IReadOnlyList<AgentToolDefinition> tools,
        string? operationalContext,
        string? continuationToken,
        IReadOnlyList<AgentToolResult> toolResults,
        CancellationToken cancellationToken)
    {
        string apiKey = await ResolveApiKeyAsync(cancellationToken);
        string instructions = BuildInstructions(
            systemPrompt,
            operationalContext);
        OpenAiToolCollection openAiTools = BuildTools(tools);

        var input = new List<object>();
        var inputItemTypes = new List<string>();
        bool isStatelessContinuation = !string.IsNullOrWhiteSpace(continuationToken);
        bool originalUserMessageIncludedInContinuation = false;
        if (string.IsNullOrWhiteSpace(continuationToken))
        {
            foreach (AiChatMessage message in history)
            {
                if (message.Role is not ("user" or "assistant") ||
                    string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                input.Add(new { role = message.Role, content = message.Content });
                inputItemTypes.Add("message");
            }

            input.Add(new { role = "user", content = userMessage });
            inputItemTypes.Add("message");
        }
        else
        {
            OpenAiContinuationState state = ReadContinuationState(continuationToken!);
            foreach (OpenAiInputMessage message in state.Messages)
            {
                input.Add(new { role = message.Role, content = message.Content });
                inputItemTypes.Add("message");
            }

            originalUserMessageIncludedInContinuation = state.Messages.Any(
                message => string.Equals(message.Role, "user", StringComparison.Ordinal));

            foreach (OpenAiFunctionCall call in state.FunctionCalls)
            {
                input.Add(new { type = "function_call", call_id = call.CallId, name = call.Name, arguments = call.Arguments });
                inputItemTypes.Add("function_call");
            }
            foreach (OpenAiFunctionCallOutput priorOutput in state.FunctionCallOutputs)
            {
                input.Add(new { type = "function_call_output", call_id = priorOutput.CallId, output = priorOutput.Output });
                inputItemTypes.Add("function_call_output");
            }
            foreach (AgentToolResult toolResult in toolResults)
            {
                input.Add(new { type = "function_call_output", call_id = toolResult.CorrelationId, output = toolResult.Content });
                inputItemTypes.Add("function_call_output");
            }
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/responses");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["instructions"] = instructions,
            ["input"] = input,
            ["temperature"] = temperature,
            ["store"] = false
        };
        string? effectiveToolChoice = null;
        bool? effectiveParallelToolCalls = null;

        if (openAiTools.Definitions.Count > 0)
        {
            requestBody["tools"] = openAiTools.Definitions;
            requestBody["tool_choice"] = "auto";
            requestBody["parallel_tool_calls"] = true;
            effectiveToolChoice = "auto";
            effectiveParallelToolCalls = true;
        }

        request.Content = JsonContent.Create(requestBody);

        logger.LogInformation(
            "OpenAI Responses request Tool diagnostics. Model: {Model}; " +
            "ReceivedToolCount: {ReceivedToolCount}; " +
            "OpenAiToolDefinitionCount: {OpenAiToolDefinitionCount}; " +
            "OpenAiFunctionNames: {OpenAiFunctionNames}; " +
            "ToolChoice: {ToolChoice}; " +
            "ParallelToolCalls: {ParallelToolCalls}; " +
            "IsStatelessContinuation: {IsStatelessContinuation}; " +
            "OriginalUserMessageIncludedInContinuation: {OriginalUserMessageIncludedInContinuation}; " +
            "FunctionCallOutputCount: {FunctionCallOutputCount}; " +
            "FunctionCallOutputCharacters: {FunctionCallOutputCharacters}; " +
            "InputItemTypes: {InputItemTypes}.",
            model,
            tools.Count,
            openAiTools.Definitions.Count,
            string.Join(", ", openAiTools.ToolIdsByFunctionName.Keys),
            effectiveToolChoice ?? "(not sent)",
            effectiveParallelToolCalls?.ToString() ?? "(not sent)",
            isStatelessContinuation,
            originalUserMessageIncludedInContinuation,
            inputItemTypes.Count(type => type == "function_call_output"),
            toolResults.Sum(result => result.Content.Length),
            string.Join(", ", inputItemTypes));

        using HttpResponseMessage response =
            await httpClient.SendAsync(request, cancellationToken);
        string responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogInformation(
            "OpenAI Responses HTTP response received. " +
            "HttpStatusCode: {HttpStatusCode}.",
            (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            OpenAiErrorDiagnostics error = ReadErrorDiagnostics(responseBody);
            logger.LogWarning(
                "OpenAI Responses request failed. HttpStatusCode: {HttpStatusCode}; " +
                "ErrorType: {ErrorType}; ErrorCode: {ErrorCode}; " +
                "ErrorParam: {ErrorParam}; ErrorMessage: {ErrorMessage}; " +
                "IsContinuation: {IsContinuation}; FunctionCallOutputCount: {FunctionCallOutputCount}; " +
                "PreviousResponseIdSent: {PreviousResponseIdSent}.",
                (int)response.StatusCode,
                error.Type ?? "(not available)",
                error.Code ?? "(not available)",
                error.Param ?? "(not available)",
                error.Message ?? "(not available)",
                !string.IsNullOrWhiteSpace(continuationToken),
                toolResults.Count,
                !string.IsNullOrWhiteSpace(continuationToken));

            throw new InvalidOperationException(
                $"OpenAI retornou {(int)response.StatusCode}.");
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);
        OpenAiResponseDiagnostics responseDiagnostics =
            ReadResponseDiagnostics(document.RootElement);

        logger.LogInformation(
            "OpenAI Responses output diagnostics. " +
            "HttpStatusCode: {HttpStatusCode}; ResponseId: {ResponseId}; " +
            "OutputTypes: {OutputTypes}; " +
            "FunctionCallCount: {FunctionCallCount}; " +
            "FunctionNames: {FunctionNames}.",
            (int)response.StatusCode,
            responseDiagnostics.ResponseId ?? "(not available)",
            string.Join(", ", responseDiagnostics.OutputTypes),
            responseDiagnostics.FunctionCallCount,
            string.Join(", ", responseDiagnostics.FunctionNames));

        string content = ReadOutputText(document.RootElement);
        IReadOnlyList<AgentToolCall> toolCalls = ReadToolCalls(
            document.RootElement,
            openAiTools.ToolIdsByFunctionName);

        logger.LogInformation(
            "OpenAI Responses Tool calls converted. " +
            "AgentToolCallCount: {AgentToolCallCount}.",
            toolCalls.Count);

        if (string.IsNullOrWhiteSpace(content) && toolCalls.Count == 0)
        {
            throw new InvalidOperationException(
                "A OpenAI retornou uma resposta vazia.");
        }

        return new AiChatCompletionResult(
            content.Trim(),
            ReadUsage(document.RootElement))
        {
            ToolCalls = toolCalls,
            ContinuationToken = CreateContinuationToken(
                continuationToken,
                toolResults,
                document.RootElement,
                history,
                userMessage)
        };
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
                "Nenhuma credencial da OpenAI estÃ¯Â¿Â½ configurada para este tenant.");
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
            "\n\nContexto operacional fornecido pela aplicaÃ¯Â¿Â½Ã¯Â¿Â½o consumidora " +
            "para esta execuÃ¯Â¿Â½Ã¯Â¿Â½o:\n" +
            operationalContext;
    }

    private static OpenAiToolCollection BuildTools(
        IReadOnlyList<AgentToolDefinition> tools)
    {
        var definitions = new List<object>(tools.Count);
        var toolIdsByFunctionName =
            new Dictionary<string, Guid>(StringComparer.Ordinal);

        for (int index = 0; index < tools.Count; index++)
        {
            AgentToolDefinition tool = tools[index];
            string functionName = $"orizon_tool_{index + 1}";
            JsonElement parameters = ParseInputSchema(tool);

            definitions.Add(new
            {
                type = "function",
                name = functionName,
                description = BuildToolDescription(tool),
                parameters,
                strict = false
            });
            toolIdsByFunctionName.Add(functionName, tool.Id);
        }

        return new OpenAiToolCollection(
            definitions,
            toolIdsByFunctionName);
    }

    private static JsonElement ParseInputSchema(
        AgentToolDefinition tool)
    {
        if (string.IsNullOrWhiteSpace(tool.InputSchema))
        {
            using JsonDocument defaultSchema = JsonDocument.Parse(
                """{"type":"object","properties":{}}""");
            return defaultSchema.RootElement.Clone();
        }

        try
        {
            using JsonDocument schema =
                JsonDocument.Parse(tool.InputSchema);

            if (schema.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"O schema de entrada da Tool {tool.Name} nÃ¯Â¿Â½o Ã¯Â¿Â½ um objeto JSON.");
            }

            return schema.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"O schema de entrada da Tool {tool.Name} nÃ¯Â¿Â½o contÃ¯Â¿Â½m JSON vÃ¯Â¿Â½lido.",
                exception);
        }
    }

    private static string BuildToolDescription(
        AgentToolDefinition tool)
    {
        return tool.Kind switch
        {
            AgentToolKind.GmailSearch =>
                "Pesquisa mensagens na conta Gmail conectada e autorizada. " +
                "A query \u00e9 opcional; quando omitida, lista mensagens recentes sem filtro. " +
                "maxResults limita a quantidade retornada. Retorna metadados seguros, " +
                "incluindo Subject e From, sem ler o corpo das mensagens.",
            AgentToolKind.GmailReadMessage =>
                "L\u00ea uma mensagem espec\u00edfica da conta Gmail conectada e autorizada. " +
                "Use somente quando o conte\u00fado da mensagem for realmente necess\u00e1rio.",
            AgentToolKind.Http =>
                $"{tool.Name}. {tool.Description} Opera\u00e7\u00e3o HTTP autorizada. " +
                $"Classifica\u00e7\u00e3o de risco: {tool.RiskLevel}.",
            _ =>
                $"{tool.Name}. {tool.Description} Opera\u00e7\u00e3o autorizada pelo sistema. " +
                $"Classifica\u00e7\u00e3o de risco: {tool.RiskLevel}."
        };
    }

    private static IReadOnlyList<AgentToolCall> ReadToolCalls(
        JsonElement root,
        IReadOnlyDictionary<string, Guid> toolIdsByFunctionName)
    {
        if (!root.TryGetProperty("output", out JsonElement output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var toolCalls = new List<AgentToolCall>();

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out JsonElement type) ||
                !string.Equals(
                    type.GetString(),
                    "function_call",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!item.TryGetProperty("name", out JsonElement nameElement) ||
                nameElement.ValueKind != JsonValueKind.String ||
                !toolIdsByFunctionName.TryGetValue(
                    nameElement.GetString()!,
                    out Guid toolId))
            {
                throw new InvalidOperationException(
                    "A OpenAI solicitou uma Tool desconhecida.");
            }

            if (!item.TryGetProperty(
                    "arguments",
                    out JsonElement argumentsElement) ||
                argumentsElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "A OpenAI retornou argumentos de Tool invÃ¯Â¿Â½lidos.");
            }

            try
            {
                using JsonDocument arguments = JsonDocument.Parse(
                    argumentsElement.GetString()!);
                string? callId = item.TryGetProperty("call_id", out JsonElement callIdElement) && callIdElement.ValueKind == JsonValueKind.String ? callIdElement.GetString() : null;

                if (string.IsNullOrWhiteSpace(callId))
                {
                    throw new InvalidOperationException("A OpenAI retornou uma Tool sem call_id.");
                }

                toolCalls.Add(new AgentToolCall(toolId, arguments.RootElement.Clone(), callId));
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "A OpenAI retornou argumentos de Tool que nÃ¯Â¿Â½o contÃ¯Â¿Â½m JSON vÃ¯Â¿Â½lido.",
                    exception);
            }
        }

        return toolCalls;
    }

    private static OpenAiResponseDiagnostics ReadResponseDiagnostics(
        JsonElement root)
    {
        string? responseId =
            root.TryGetProperty("id", out JsonElement id) &&
            id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        var outputTypes = new List<string>();
        var functionNames = new List<string>();
        int functionCallCount = 0;

        if (root.TryGetProperty("output", out JsonElement output) &&
            output.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out JsonElement typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(typeElement.GetString()))
                {
                    continue;
                }

                string type = typeElement.GetString()!;
                if (!outputTypes.Contains(type, StringComparer.Ordinal))
                {
                    outputTypes.Add(type);
                }

                if (!string.Equals(
                        type,
                        "function_call",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                functionCallCount++;
                if (item.TryGetProperty("name", out JsonElement nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(nameElement.GetString()))
                {
                    functionNames.Add(nameElement.GetString()!);
                }
            }
        }

        return new OpenAiResponseDiagnostics(
            responseId,
            outputTypes,
            functionCallCount,
            functionNames);
    }

    private static string CreateContinuationToken(
        string? previousToken,
        IReadOnlyList<AgentToolResult> results,
        JsonElement response,
        IReadOnlyList<AiChatMessage> history,
        string userMessage)
    {
        OpenAiContinuationState state = string.IsNullOrWhiteSpace(previousToken)
            ? new(
                history
                    .Where(message =>
                        message.Role is "user" or "assistant" &&
                        !string.IsNullOrWhiteSpace(message.Content))
                    .Select(message => new OpenAiInputMessage(
                        message.Role,
                        message.Content))
                    .Append(new OpenAiInputMessage("user", userMessage))
                    .ToList(),
                [],
                [])
            : ReadContinuationState(previousToken);
        var calls = state.FunctionCalls.ToList();
        var outputs = state.FunctionCallOutputs.Concat(results.Select(result => new OpenAiFunctionCallOutput(result.CorrelationId, result.Content))).ToList();
        if (response.TryGetProperty("output", out JsonElement items) && items.ValueKind == JsonValueKind.Array)
        foreach (JsonElement item in items.EnumerateArray())
        if (ReadString(item, "type") == "function_call" && ReadString(item, "call_id") is { Length: > 0 } callId && ReadString(item, "name") is { Length: > 0 } name && ReadString(item, "arguments") is { } arguments)
            calls.Add(new OpenAiFunctionCall(callId, name, arguments));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new OpenAiContinuationState(
                state.Messages,
                calls,
                outputs))));
    }

    private static OpenAiContinuationState ReadContinuationState(string token)
    {
        try { return JsonSerializer.Deserialize<OpenAiContinuationState>(Encoding.UTF8.GetString(Convert.FromBase64String(token))) ?? throw new InvalidOperationException("Estado de continuação OpenAI inválido."); }
        catch (Exception exception) when (exception is FormatException or JsonException) { throw new InvalidOperationException("Estado de continuação OpenAI inválido.", exception); }
    }
    private static OpenAiErrorDiagnostics ReadErrorDiagnostics(
        string responseBody)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("error", out JsonElement error) ||
                error.ValueKind != JsonValueKind.Object)
            {
                return new OpenAiErrorDiagnostics(null, null, null, null);
            }

            return new OpenAiErrorDiagnostics(
                ReadString(error, "type"),
                ReadString(error, "code"),
                ReadString(error, "param"),
                TruncateForLog(ReadString(error, "message")));
        }
        catch (JsonException)
        {
            return new OpenAiErrorDiagnostics(null, null, null, null);
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? TruncateForLog(string? value) =>
        value is null ? null : value.Length <= 500 ? value : value[..500];
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

    private sealed record OpenAiToolCollection(
        IReadOnlyList<object> Definitions,
        IReadOnlyDictionary<string, Guid> ToolIdsByFunctionName);

    private sealed record OpenAiInputMessage(string Role, string Content);
    private sealed record OpenAiFunctionCall(string CallId, string Name, string Arguments);
    private sealed record OpenAiFunctionCallOutput(string CallId, string Output);
    private sealed record OpenAiContinuationState(
        IReadOnlyList<OpenAiInputMessage> Messages,
        IReadOnlyList<OpenAiFunctionCall> FunctionCalls,
        IReadOnlyList<OpenAiFunctionCallOutput> FunctionCallOutputs);
    private sealed record OpenAiErrorDiagnostics(
        string? Type,
        string? Code,
        string? Param,
        string? Message);

    private sealed record OpenAiResponseDiagnostics(
        string? ResponseId,
        IReadOnlyList<string> OutputTypes,
        int FunctionCallCount,
        IReadOnlyList<string> FunctionNames);
}
