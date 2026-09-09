using System.Globalization;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Infrastructure.Integrations.Gmail;

public sealed class GmailClient(
    IHttpClientFactory clients,
    IGoogleOAuthTokenService tokens,
    IGmailMessageContentReducer contentReducer) : IGmailClient
{
    public const string HttpClientName = "Gmail";

    public async Task<GmailSentMessage> SendDraftAsync(
        Guid connectionId,
        string draftId,
        CancellationToken cancellationToken = default)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("ConnectionId \u00e9 obrigat\u00f3rio.", nameof(connectionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        string normalizedDraftId = draftId.Trim();

        var tokenResult = await tokens.GetAccessTokenAsync(connectionId, cancellationToken);
        if (!tokenResult.Succeeded || tokenResult.Value is null)
        {
            throw new InvalidOperationException(
                tokenResult.FirstError ?? "N\u00e3o foi poss\u00edvel obter o token Google.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://gmail.googleapis.com/gmail/v1/users/me/drafts/send")
        {
            Content = JsonContent.Create(new { id = normalizedDraftId })
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.Value.Value);

        using var client = clients.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GmailApiException(response.StatusCode);
        }

        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        string? messageId = ReadString(json.RootElement, "id");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("A API Gmail n\u00e3o retornou o identificador da mensagem enviada.");
        }

        return new GmailSentMessage(
            messageId,
            ReadString(json.RootElement, "threadId"));
    }

    public async Task<GmailDraft> CreateDraftAsync(
        Guid connectionId,
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("ConnectionId \u00e9 obrigat\u00f3rio.", nameof(connectionId));
        }

        string recipient = NormalizeRecipient(to);
        string normalizedSubject = NormalizeHeaderValue(subject, nameof(subject));
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var tokenResult = await tokens.GetAccessTokenAsync(connectionId, cancellationToken);
        if (!tokenResult.Succeeded || tokenResult.Value is null)
        {
            throw new InvalidOperationException(
                tokenResult.FirstError ?? "N\u00e3o foi poss\u00edvel obter o token Google.");
        }

        string rawMime = BuildPlainTextMime(recipient, normalizedSubject, body);
        string raw = Base64UrlEncode(Encoding.UTF8.GetBytes(rawMime));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://gmail.googleapis.com/gmail/v1/users/me/drafts")
        {
            Content = JsonContent.Create(new { raw })
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.Value.Value);

        using var client = clients.CreateClient(HttpClientName);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GmailApiException(response.StatusCode);
        }

        using JsonDocument json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        string? draftId = ReadString(json.RootElement, "id");
        if (string.IsNullOrWhiteSpace(draftId))
        {
            throw new InvalidOperationException("A API Gmail n\u00e3o retornou o identificador do rascunho.");
        }

        JsonElement message;
        return new GmailDraft(
            draftId,
            json.RootElement.TryGetProperty("message", out message) &&
            message.ValueKind == JsonValueKind.Object ? ReadString(message, "id") : null,
            json.RootElement.TryGetProperty("message", out message) &&
            message.ValueKind == JsonValueKind.Object ? ReadString(message, "threadId") : null);
    }

    private static string NormalizeRecipient(string value)
    {
        string candidate = NormalizeHeaderValue(value, nameof(value));
        try
        {
            var address = new MailAddress(candidate);
            if (!string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Destinat\u00e1rio Gmail inv\u00e1lido.", nameof(value));
            }

            return address.Address;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Destinat\u00e1rio Gmail inv\u00e1lido.", nameof(value), exception);
        }
    }

    private static string NormalizeHeaderValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        string normalized = value.Trim();
        if (normalized.Contains('\r') || normalized.Contains('\n'))
        {
            throw new ArgumentException("Cabe\u00e7alho Gmail inv\u00e1lido.", parameterName);
        }

        return normalized;
    }

    private static string BuildPlainTextMime(string to, string subject, string body) =>
        $"To: {to}\r\n" +
        $"Subject: =?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain; charset=utf-8\r\n" +
        "Content-Transfer-Encoding: base64\r\n\r\n" +
        Convert.ToBase64String(Encoding.UTF8.GetBytes(body));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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

        var queryParameters = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            queryParameters["q"] = query.Trim();
        }

        queryParameters["maxResults"] = maxResults.ToString(
            CultureInfo.InvariantCulture);

        string url = QueryHelpers.AddQueryString(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages",
            queryParameters);

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

        var messagesWithMetadata = new List<GmailMessageReference>(messages.Count);
        foreach (GmailMessageReference message in messages)
        {
            messagesWithMetadata.Add(await GetMessageMetadataAsync(
                client, tokenResult.Value.Value, message, cancellationToken));
        }

        string? nextPageToken = ReadString(root, "nextPageToken");

        long? resultSizeEstimate = null;

        if (root.TryGetProperty("resultSizeEstimate", out var estimate) &&
            estimate.TryGetInt64(out long value))
        {
            resultSizeEstimate = value;
        }

        return new GmailSearchResult(
            messagesWithMetadata,
            nextPageToken,
            resultSizeEstimate);
    }

    private static async Task<GmailMessageReference> GetMessageMetadataAsync(
        HttpClient client,
        string accessToken,
        GmailMessageReference message,
        CancellationToken cancellationToken)
    {
        string url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(message.Id)}?format=metadata&metadataHeaders=Subject&metadataHeaders=From";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GmailApiException(response.StatusCode);
        }

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement root = json.RootElement;
        JsonElement payload;
        string? subject = root.TryGetProperty("payload", out payload) &&
            payload.ValueKind == JsonValueKind.Object
            ? ReadHeader(payload, "Subject")
            : null;
        string? from = root.TryGetProperty("payload", out payload) &&
            payload.ValueKind == JsonValueKind.Object
            ? ReadHeader(payload, "From")
            : null;

        return new GmailMessageReference(message.Id, message.ThreadId, subject, from);
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
            bool usesHtml = bodies.PlainText is null;
            bodyText = contentReducer.Reduce(
                bodies.PlainText ?? bodies.Html,
                usesHtml);
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
