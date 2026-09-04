using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Infrastructure.Integrations.Gmail;

public sealed class GmailClient(
    IHttpClientFactory clients,
    IGoogleOAuthTokenService tokens) : IGmailClient
{
    public const string HttpClientName = "Gmail";

    public async Task<GmailSearchResult> SearchMessagesAsync(
        Guid connectionId,
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("ConnectionId é obrigatório.", nameof(connectionId));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("A consulta é obrigatória.", nameof(query));
        }

        if (maxResults is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResults),
                "MaxResults deve estar entre 1 e 100.");
        }

        var tokenResult = await tokens.GetAccessTokenAsync(
            connectionId,
            cancellationToken);

        if (!tokenResult.Succeeded || tokenResult.Value is null)
        {
            throw new InvalidOperationException(
                tokenResult.FirstError ?? "Não foi possível obter o token Google.");
        }

        string url = QueryHelpers.AddQueryString(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages",
            new Dictionary<string, string?>
            {
                ["q"] = query.Trim(),
                ["maxResults"] = maxResults.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.Value.Value);

        using var client = clients.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GmailApiException(response.StatusCode);
        }

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        var root = json.RootElement;

        var messages = new List<GmailMessageReference>();

        if (root.TryGetProperty("messages", out var messageArray) &&
            messageArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in messageArray.EnumerateArray())
            {
                string? id = ReadString(item, "id");
                string? threadId = ReadString(item, "threadId");

                if (!string.IsNullOrWhiteSpace(id) &&
                    !string.IsNullOrWhiteSpace(threadId))
                {
                    messages.Add(new GmailMessageReference(id, threadId));
                }
            }
        }

        string? nextPageToken = ReadString(root, "nextPageToken");

        long? resultSizeEstimate = null;

        if (root.TryGetProperty("resultSizeEstimate", out var estimate) &&
            estimate.TryGetInt64(out long value))
        {
            resultSizeEstimate = value;
        }

        return new GmailSearchResult(
            messages,
            nextPageToken,
            resultSizeEstimate);
    }

    public Task<GmailMessage> GetMessageAsync(
        Guid connectionId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed class GmailApiException(System.Net.HttpStatusCode statusCode)
    : Exception($"Falha na comunicação com a API Gmail ({(int)statusCode}).")
{
    public System.Net.HttpStatusCode StatusCode { get; } = statusCode;
}