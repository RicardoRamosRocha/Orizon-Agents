using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrizonAgents.Infrastructure.Tools.Execution;

internal static class ToolExecutionInputHasher
{
    public static string Compute(JsonElement? input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Canonicalize(input)));

        return Convert.ToHexString(hash);
    }

    public static string Canonicalize(JsonElement? input)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (input.HasValue)
            {
                WriteCanonical(writer, input.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string? CanonicalizeSchema(string? inputSchema)
    {
        if (inputSchema is null)
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(inputSchema);
            return Canonicalize(document.RootElement);
        }
        catch (JsonException)
        {
            throw new ArgumentException(
                "The Tool input schema is invalid.",
                nameof(inputSchema));
        }
    }

    private static void WriteCanonical(
        Utf8JsonWriter writer,
        JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (JsonProperty property in element
                    .EnumerateObject()
                    .OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(
                    element.GetRawText(),
                    skipInputValidation: false);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException(
                    $"Tipo JSON não suportado: {element.ValueKind}.");
        }
    }
}
