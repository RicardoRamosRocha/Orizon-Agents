using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Integrations.Gmail;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class GmailAgentToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IGmailClient _gmailClient;
    private readonly ILogger<GmailAgentToolExecutor> _logger;

    public GmailAgentToolExecutor(
        IGmailClient gmailClient,
        ILogger<GmailAgentToolExecutor> logger)
    {
        _gmailClient = gmailClient;
        _logger = logger;
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(
        AgentTool tool,
        JsonElement? input,
        CancellationToken cancellationToken = default)
    {
        if (tool.Kind is not
            (AgentToolKind.GmailSearch or AgentToolKind.GmailReadMessage))
        {
            return AgentToolExecutionResult.Failure(
                "A Tool informada não é uma Tool Gmail.");
        }

        if (!tool.IntegrationConnectionId.HasValue ||
            tool.IntegrationConnectionId == Guid.Empty)
        {
            return AgentToolExecutionResult.Failure(
                "A conexão configurada para a Tool Gmail não está disponível.");
        }

        if (!input.HasValue ||
            input.Value.ValueKind != JsonValueKind.Object)
        {
            return InvalidArguments();
        }

        try
        {
            return tool.Kind switch
            {
                AgentToolKind.GmailSearch =>
                    await ExecuteSearchAsync(
                        tool.IntegrationConnectionId.Value,
                        input.Value,
                        cancellationToken),

                AgentToolKind.GmailReadMessage =>
                    await ExecuteReadMessageAsync(
                        tool.IntegrationConnectionId.Value,
                        input.Value,
                        cancellationToken),

                _ => AgentToolExecutionResult.Failure(
                    "O tipo configurado para a Tool Gmail não é suportado.")
            };
        }
        catch (GmailApiException exception)
        {
            _logger.LogWarning(
                "API Gmail recusou a execução da Tool {ToolId}. StatusCode: {StatusCode}.",
                tool.Id,
                (int)exception.StatusCode);

            return AgentToolExecutionResult.Failure(
                "Não foi possível executar a Tool Gmail.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return AgentToolExecutionResult.Failure(
                "A execução da Tool Gmail excedeu o tempo limite.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Erro ao executar Tool Gmail {ToolId}. Tipo: {ExceptionType}.",
                tool.Id,
                exception.GetType().Name);

            return AgentToolExecutionResult.Failure(
                "Não foi possível executar a Tool Gmail.");
        }
    }

    private async Task<AgentToolExecutionResult> ExecuteSearchAsync(
        Guid connectionId,
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!TryReadRequiredString(input, "query", out string query))
        {
            return InvalidArguments();
        }

        int maxResults = 10;

        if (input.TryGetProperty("maxResults", out JsonElement maxResultsElement))
        {
            if (!maxResultsElement.TryGetInt32(out maxResults) ||
                maxResults is < 1 or > 100)
            {
                return InvalidArguments();
            }
        }

        GmailSearchResult result =
            await _gmailClient.SearchMessagesAsync(
                connectionId,
                query,
                maxResults,
                cancellationToken);

        return AgentToolExecutionResult.Success(
            null,
            JsonSerializer.Serialize(result, JsonOptions));
    }

    private async Task<AgentToolExecutionResult> ExecuteReadMessageAsync(
        Guid connectionId,
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!TryReadRequiredString(input, "messageId", out string messageId))
        {
            return InvalidArguments();
        }

        GmailMessage result =
            await _gmailClient.GetMessageAsync(
                connectionId,
                messageId,
                cancellationToken);

        return AgentToolExecutionResult.Success(
            null,
            JsonSerializer.Serialize(result, JsonOptions));
    }

    private static bool TryReadRequiredString(
        JsonElement input,
        string propertyName,
        out string value)
    {
        value = string.Empty;

        if (!input.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? candidate = property.GetString();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate.Trim();
        return true;
    }

    private static AgentToolExecutionResult InvalidArguments() =>
        AgentToolExecutionResult.Failure(
            "Os argumentos fornecidos para a Tool Gmail são inválidos.");
}
