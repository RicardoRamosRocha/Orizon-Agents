using OrizonAgents.Application.Integrations;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class GmailRecipientResolver(IIntegrationConnectionService connections)
{
    public async Task<GmailRecipientResolution> ResolveAsync(
        Guid connectionId,
        string requestedRecipient,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnectedAccountAlias(requestedRecipient))
        {
            return GmailRecipientResolution.Success(requestedRecipient);
        }

        var connection = await connections.GetAsync(connectionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(connection?.ConnectedAccountEmail))
        {
            return GmailRecipientResolution.Failure(
                "A conexão Gmail não possui e-mail da conta conectada para resolver o destinatário.");
        }

        return GmailRecipientResolution.Success(connection.ConnectedAccountEmail);
    }

    private static bool IsConnectedAccountAlias(string value) =>
        value.Trim().ToUpperInvariant() is "MEU E-MAIL" or "MEU EMAIL" or "PARA MINHA CONTA";
}

public sealed record GmailRecipientResolution(
    bool Succeeded,
    string? Recipient,
    string? Error)
{
    public static GmailRecipientResolution Success(string recipient) =>
        new(true, recipient, null);

    public static GmailRecipientResolution Failure(string error) =>
        new(false, null, error);
}
