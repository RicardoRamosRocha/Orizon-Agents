using System.Globalization;
using System.Text.Json;
using OrizonAgents.Application.Tools.Validation;

namespace OrizonAgents.Infrastructure.Tools.Validation;

public sealed class AgentToolInputValidator : IAgentToolInputValidator
{
    public AgentToolInputValidationResult Validate(
        string? inputSchema,
        JsonElement? input)
    {
        if (string.IsNullOrWhiteSpace(inputSchema))
        {
            return AgentToolInputValidationResult.Success();
        }

        JsonDocument schemaDocument;

        try
        {
            schemaDocument = JsonDocument.Parse(inputSchema);
        }
        catch (JsonException)
        {
            return AgentToolInputValidationResult.Failure(
                new[] { "O InputSchema configurado para a Tool é inválido." });
        }

        using (schemaDocument)
        {
            var errors = new List<string>();

            ValidateElement(
                schemaDocument.RootElement,
                input,
                "$",
                errors);

            return errors.Count == 0
                ? AgentToolInputValidationResult.Success()
                : AgentToolInputValidationResult.Failure(errors);
        }
    }

    private static void ValidateElement(
        JsonElement schema,
        JsonElement? value,
        string path,
        ICollection<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"Schema inválido em {path}.");
            return;
        }

        if (!schema.TryGetProperty("type", out JsonElement typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            errors.Add($"O schema em {path} deve declarar um type.");
            return;
        }

        string? expectedType = typeElement.GetString();

        if (!value.HasValue)
        {
            errors.Add($"Entrada obrigatória ausente em {path}.");
            return;
        }

        JsonElement actualValue = value.Value;

        if (!MatchesType(expectedType, actualValue))
        {
            errors.Add(
                $"Tipo inválido em {path}. Esperado: {expectedType}.");
            return;
        }

        switch (expectedType)
        {
            case "object":
                ValidateObject(schema, actualValue, path, errors);
                break;

            case "array":
                ValidateArray(schema, actualValue, path, errors);
                break;

            case "string":
                ValidateString(schema, actualValue, path, errors);
                break;
        }
    }

    private static void ValidateObject(
        JsonElement schema,
        JsonElement value,
        string path,
        ICollection<string> errors)
    {
        var required = new HashSet<string>(
            StringComparer.Ordinal);

        if (schema.TryGetProperty("required", out JsonElement requiredElement) &&
            requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in requiredElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    required.Add(item.GetString()!);
                }
            }
        }

        foreach (string propertyName in required)
        {
            if (!value.TryGetProperty(propertyName, out _))
            {
                errors.Add(
                    $"Campo obrigatório ausente: {path}.{propertyName}.");
            }
        }

        bool disallowAdditionalProperties =
            schema.TryGetProperty(
                "additionalProperties",
                out JsonElement additionalPropertiesElement) &&
            additionalPropertiesElement.ValueKind == JsonValueKind.False;

        bool hasProperties =
            schema.TryGetProperty(
                "properties",
                out JsonElement propertiesElement) &&
            propertiesElement.ValueKind == JsonValueKind.Object;

        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!hasProperties ||
                !propertiesElement.TryGetProperty(
                    property.Name,
                    out JsonElement propertySchema))
            {
                if (disallowAdditionalProperties)
                {
                    errors.Add(
                        $"Campo não permitido: {path}.{property.Name}.");
                }

                continue;
            }

            ValidateElement(
                propertySchema,
                property.Value,
                $"{path}.{property.Name}",
                errors);
        }
    }

    private static void ValidateArray(
        JsonElement schema,
        JsonElement value,
        string path,
        ICollection<string> errors)
    {
        if (!schema.TryGetProperty(
                "items",
                out JsonElement itemSchema))
        {
            return;
        }

        int index = 0;

        foreach (JsonElement item in value.EnumerateArray())
        {
            ValidateElement(
                itemSchema,
                item,
                $"{path}[{index}]",
                errors);

            index++;
        }
    }

    private static void ValidateString(
        JsonElement schema,
        JsonElement value,
        string path,
        ICollection<string> errors)
    {
        if (!schema.TryGetProperty(
                "format",
                out JsonElement formatElement) ||
            formatElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        string? format = formatElement.GetString();
        string stringValue = value.GetString() ?? string.Empty;

        if (format == "date" &&
            !DateOnly.TryParseExact(
                stringValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            errors.Add(
                $"Formato de data inválido em {path}. Use yyyy-MM-dd.");
        }
    }

    private static bool MatchesType(
        string? expectedType,
        JsonElement value)
    {
        return expectedType switch
        {
            "object" =>
                value.ValueKind == JsonValueKind.Object,

            "array" =>
                value.ValueKind == JsonValueKind.Array,

            "string" =>
                value.ValueKind == JsonValueKind.String,

            "number" =>
                value.ValueKind == JsonValueKind.Number,

            "integer" =>
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out _),

            "boolean" =>
                value.ValueKind is
                    JsonValueKind.True or JsonValueKind.False,

            "null" =>
                value.ValueKind == JsonValueKind.Null,

            _ => false
        };
    }
}
