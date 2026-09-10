using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Integrations.Gmail;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class GmailAgentToolExecutor
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IGmailClient _gmailClient;
    private readonly IGoogleOAuthCapabilityService _capabilities;
    private readonly ILogger<GmailAgentToolExecutor> _logger;

    public GmailAgentToolExecutor(
        IGmailClient gmailClient,
        IGoogleOAuthCapabilityService capabilities,
        ILogger<GmailAgentToolExecutor> logger)
    {
        _gmailClient = gmailClient;
        _capabilities = capabilities;
        _logger = logger;
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(
        AgentTool tool,
        JsonElement? input,
        CancellationToken cancellationToken = default)
    {
        if (!GmailToolPolicy.IsGmail(tool.Kind))
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

                AgentToolKind.GmailCreateDraft =>
                    await ExecuteCreateDraftAsync(
                        tool.IntegrationConnectionId.Value,
                        input.Value,
                        cancellationToken),

                AgentToolKind.GmailSend =>
                    await ExecuteSendDraftAsync(
                        tool.IntegrationConnectionId.Value,
                        input.Value,
                        cancellationToken),

                AgentToolKind.GmailReply =>
                    await ExecuteReplyAsync(
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
        string query = string.Empty;
        if (input.TryGetProperty("query", out _) &&
            !TryReadRequiredString(input, "query", out query))
        {
            return InvalidArguments();
        }

        int maxResults = Math.Min(10, GmailToolPolicy.MaximumSearchResultsForAgent);

        if (input.TryGetProperty("maxResults", out JsonElement maxResultsElement))
        {
            if (!maxResultsElement.TryGetInt32(out maxResults) ||
                maxResults is < 1 or > GmailToolPolicy.MaximumSearchResultsForAgent)
            {
                return InvalidArguments();
            }
        }

        if (!await HasRequiredCapabilityAsync(
                connectionId, AgentToolKind.GmailSearch, cancellationToken))
        {
            return MissingGmailAuthorization();
        }

        GmailSearchResult result =
            await _gmailClient.SearchMessagesAsync(
                connectionId,
                query,
                maxResults,
                cancellationToken);

        _logger.LogInformation(
            "GmailSearch completed. ToolExecutionSucceeded: true; GmailSearchResultCount: {GmailSearchResultCount}; HasSubjectCount: {HasSubjectCount}; HasFromCount: {HasFromCount}.",
            result.Messages.Count,
            result.Messages.Count(message => !string.IsNullOrWhiteSpace(message.Subject)),
            result.Messages.Count(message => !string.IsNullOrWhiteSpace(message.From)));

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

        if (!await HasRequiredCapabilityAsync(
                connectionId, AgentToolKind.GmailReadMessage, cancellationToken))
        {
            return MissingGmailAuthorization();
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

    private async Task<AgentToolExecutionResult> ExecuteUnavailableWriteOperationAsync(
        Guid connectionId,
        AgentToolKind kind,
        CancellationToken cancellationToken)
    {
        if (!await HasRequiredCapabilityAsync(connectionId, kind, cancellationToken))
        {
            return MissingGmailAuthorization();
        }

        return AgentToolExecutionResult.Failure(
            "A opera\u00e7\u00e3o de escrita Gmail ainda n\u00e3o est\u00e1 dispon\u00edvel.");
    }

    private async Task<AgentToolExecutionResult> ExecuteCreateDraftAsync(
        Guid connectionId,
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!TryReadRequiredString(input, "to", out string to) ||
            !TryReadRequiredString(input, "subject", out string subject) ||
            !TryReadRequiredString(input, "body", out string body))
        {
            return InvalidArguments();
        }

        if (!await HasRequiredCapabilityAsync(
                connectionId, AgentToolKind.GmailCreateDraft, cancellationToken))
        {
            return MissingGmailAuthorization();
        }

        GmailDraft draft = await _gmailClient.CreateDraftAsync(
            connectionId, to, subject, body, cancellationToken);
        return AgentToolExecutionResult.Success(
            null,
            JsonSerializer.Serialize(draft, JsonOptions));
    }

    private async Task<AgentToolExecutionResult> ExecuteSendDraftAsync(
        Guid connectionId,
        JsonElement input,
        CancellationToken cancellationToken)
    {
        if (!TryReadRequiredString(input, "draftId", out string draftId))
        {
            return InvalidArguments();
        }

        if (!await HasRequiredCapabilityAsync(
                connectionId, AgentToolKind.GmailSend, cancellationToken))
        {
            return MissingGmailAuthorization();
        }

        GmailSentMessage sent = await _gmailClient.SendDraftAsync(
            connectionId, draftId, cancellationToken);
        return AgentToolExecutionResult.Success(
            null,
            JsonSerializer.Serialize(sent, JsonOptions));
    }

    private async Task<AgentToolExecutionResult> ExecuteReplyAsync(Guid connectionId, JsonElement input, CancellationToken cancellationToken)
    {
        if (!TryReadRequiredString(input, "messageId", out string messageId) ||
            !TryReadRequiredString(input, "body", out string body)) return InvalidArguments();
        if (!await HasRequiredCapabilityAsync(connectionId, AgentToolKind.GmailReply, cancellationToken))
            return MissingGmailAuthorization();
        GmailReplyMessage reply = await _gmailClient.ReplyAsync(connectionId, messageId, body, cancellationToken);
        return AgentToolExecutionResult.Success(null, JsonSerializer.Serialize(reply, JsonOptions));
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

    private static GoogleOAuthCapability RequiredCapability(AgentToolKind kind) => kind switch
    {
        AgentToolKind.GmailSearch or AgentToolKind.GmailReadMessage =>
            GoogleOAuthCapability.GmailRead,
        AgentToolKind.GmailCreateDraft => GoogleOAuthCapability.GmailCreateDraft,
        AgentToolKind.GmailSend => GoogleOAuthCapability.GmailSend,
        AgentToolKind.GmailReply => GoogleOAuthCapability.GmailReply,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private Task<bool> HasRequiredCapabilityAsync(
        Guid connectionId,
        AgentToolKind kind,
        CancellationToken cancellationToken) =>
        _capabilities.HasCapabilityAsync(
            connectionId,
            RequiredCapability(kind),
            cancellationToken);

    private static AgentToolExecutionResult MissingGmailAuthorization() =>
        AgentToolExecutionResult.Failure(
            "A conexão Google não possui autorização necessária para leitura do Gmail.");
}
