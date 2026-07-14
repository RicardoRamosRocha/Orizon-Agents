using System.Text.Json;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed record ParsedWhatsAppWebhook(string EventId, string? PhoneNumberId, IReadOnlyCollection<ParsedIncomingMessage> Messages, IReadOnlyCollection<ParsedStatusUpdate> Statuses);

public sealed record ParsedIncomingMessage(string ExternalMessageId, string From, string To, WhatsAppMessageType Type, string? Text, string? MediaId, long TimestampUnix);

public sealed record ParsedStatusUpdate(string ExternalMessageId, WhatsAppMessageStatus Status, string Recipient, string? ErrorCode, string? ErrorMessage, long TimestampUnix);

public static class WhatsAppWebhookParser
{
    public static ParsedWhatsAppWebhook Parse(string rawBody)
    {
        using JsonDocument document = JsonDocument.Parse(rawBody);
        var messages = new List<ParsedIncomingMessage>();
        var statuses = new List<ParsedStatusUpdate>();
        string? phoneNumberId = null;
        string? firstId = null;

        if (!document.RootElement.TryGetProperty("entry", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return new ParsedWhatsAppWebhook(Hash(rawBody), null, messages, statuses);
        }

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out JsonElement changes) || changes.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out JsonElement value)) continue;
                if (value.TryGetProperty("metadata", out JsonElement metadata))
                {
                    phoneNumberId ??= GetString(metadata, "phone_number_id");
                }

                string to = value.TryGetProperty("metadata", out JsonElement meta) ? GetString(meta, "display_phone_number") ?? string.Empty : string.Empty;
                if (value.TryGetProperty("messages", out JsonElement messageArray) && messageArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in messageArray.EnumerateArray())
                    {
                        string id = GetString(item, "id") ?? Hash(item.GetRawText());
                        firstId ??= id;
                        string typeRaw = GetString(item, "type") ?? "unknown";
                        WhatsAppMessageType type = ParseType(typeRaw);
                        string? text = type == WhatsAppMessageType.Text && item.TryGetProperty("text", out JsonElement textElement) ? GetString(textElement, "body") : null;
                        string? mediaId = TryGetNestedId(item, typeRaw);
                        long timestamp = long.TryParse(GetString(item, "timestamp"), out long parsedTimestamp) ? parsedTimestamp : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        messages.Add(new ParsedIncomingMessage(id, GetString(item, "from") ?? string.Empty, to, type, text, mediaId, timestamp));
                    }
                }

                if (value.TryGetProperty("statuses", out JsonElement statusArray) && statusArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in statusArray.EnumerateArray())
                    {
                        string id = GetString(item, "id") ?? Hash(item.GetRawText());
                        firstId ??= id;
                        string statusRaw = GetString(item, "status") ?? "unknown";
                        string? errorCode = null;
                        string? errorMessage = null;
                        if (item.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array)
                        {
                            JsonElement first = errors.EnumerateArray().FirstOrDefault();
                            errorCode = GetString(first, "code");
                            errorMessage = GetString(first, "message") ?? GetString(first, "title");
                        }

                        long timestamp = long.TryParse(GetString(item, "timestamp"), out long parsedTimestamp) ? parsedTimestamp : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        statuses.Add(new ParsedStatusUpdate(id, ParseStatus(statusRaw), GetString(item, "recipient_id") ?? string.Empty, errorCode, errorMessage, timestamp));
                    }
                }
            }
        }

        string eventId = firstId ?? Hash(rawBody);
        return new ParsedWhatsAppWebhook(eventId, phoneNumberId, messages, statuses);
    }

    public static DateTime FromUnix(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(Math.Max(0, unix)).UtcDateTime;

    private static string? TryGetNestedId(JsonElement item, string type)
        => item.TryGetProperty(type, out JsonElement nested) ? GetString(nested, "id") : null;

    private static WhatsAppMessageType ParseType(string type)
        => type.ToLowerInvariant() switch
        {
            "text" => WhatsAppMessageType.Text,
            "template" => WhatsAppMessageType.Template,
            "image" => WhatsAppMessageType.Image,
            "document" => WhatsAppMessageType.Document,
            "audio" => WhatsAppMessageType.Audio,
            "video" => WhatsAppMessageType.Video,
            "button" => WhatsAppMessageType.Button,
            "interactive" => WhatsAppMessageType.Interactive,
            _ => WhatsAppMessageType.Unknown
        };

    private static WhatsAppMessageStatus ParseStatus(string status)
        => status.ToLowerInvariant() switch
        {
            "sent" => WhatsAppMessageStatus.Sent,
            "delivered" => WhatsAppMessageStatus.Delivered,
            "read" => WhatsAppMessageStatus.Read,
            "failed" => WhatsAppMessageStatus.Failed,
            _ => WhatsAppMessageStatus.Received
        };

    private static string? GetString(JsonElement element, string propertyName)
        => element.ValueKind != JsonValueKind.Undefined && element.TryGetProperty(propertyName, out JsonElement value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static string Hash(string value)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
