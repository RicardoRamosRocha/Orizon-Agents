using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
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

    public async Task<GmailMessage> GetMessageAsync(
        Guid connectionId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("ConnectionId é obrigatório.", nameof(connectionId));
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("MessageId é obrigatório.", nameof(messageId));
        }

        var tokenResult = await tokens.GetAccessTokenAsync(
            connectionId,
            cancellationToken);

        if (!tokenResult.Succeeded || tokenResult.Value is null)
        {
            throw new InvalidOperationException(
                tokenResult.FirstError ?? "Não foi possível obter o token Google.");
        }

        string url =
            $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(messageId.Trim())}?format=full";

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
        string? subject = null;
        string? from = null;
        string? to = null;
        DateTimeOffset? date = null;
        string? bodyText = null;

        if (root.TryGetProperty("payload", out var payload) &&
            payload.ValueKind == JsonValueKind.Object)
        {
            subject = ReadHeader(payload, "Subject");
            from = ReadHeader(payload, "From");
            to = ReadHeader(payload, "To");

            string? dateHeader = ReadHeader(payload, "Date");
            if (DateTimeOffset.TryParse(
                dateHeader,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDate))
            {
                date = parsedDate;
            }

            var bodies = FindBodies(payload);
            bodyText = bodies.PlainText ?? bodies.Html;
        }

        return new GmailMessage(
            ReadString(root, "id") ?? string.Empty,
            ReadString(root, "threadId") ?? string.Empty,
            subject,
            from,
            to,
            date,
            ReadString(root, "snippet"),
            bodyText);
    }

    private static string? ReadHeader(JsonElement payload, string headerName)
    {
        if (!payload.TryGetProperty("headers", out var headers) ||
            headers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var header in headers.EnumerateArray())
        {
            if (string.Equals(
                ReadString(header, "name"),
                headerName,
                StringComparison.OrdinalIgnoreCase))
            {
                return ReadString(header, "value");
            }
        }

        return null;
    }

    private static (string? PlainText, string? Html) FindBodies(JsonElement part)
    {
        string? plainText = null;
        string? html = null;
        FindBodies(part, ref plainText, ref html);
        return (plainText, html);
    }

    private static void FindBodies(
        JsonElement part,
        ref string? plainText,
        ref string? html)
    {
        string? mimeType = ReadString(part, "mimeType");

        if ((plainText is null &&
             string.Equals(mimeType, "text/plain", StringComparison.OrdinalIgnoreCase)) ||
            (html is null &&
             string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase)))
        {
            string? data = null;
            if (part.TryGetProperty("body", out var body) &&
                body.ValueKind == JsonValueKind.Object)
            {
                data = ReadString(body, "data");
            }

            if (data is not null)
            {
                string decoded = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(data));

                if (string.Equals(mimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
                {
                    plainText = decoded;
                }
                else
                {
                    html = decoded;
                }
            }
        }

        if (!part.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var child in parts.EnumerateArray())
        {
            FindBodies(child, ref plainText, ref html);
        }
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
