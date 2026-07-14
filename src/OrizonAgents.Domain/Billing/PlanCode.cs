using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OrizonAgents.Domain.Billing;

public static partial class PlanCode
{
    public const int MaxLength = 64;
    public const string Legacy = "LEGACY";

    public static string Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = RemoveDiacritics(value.Trim().ToUpperInvariant());
        normalized = InvalidCharactersRegex().Replace(normalized, "_");
        normalized = RepeatedSeparatorsRegex().Replace(normalized, "_").Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Plan code must contain at least one letter or number.", nameof(value));
        }

        return normalized.Length <= MaxLength
            ? normalized
            : normalized[..MaxLength].Trim('_');
    }

    private static string RemoveDiacritics(string value)
    {
        string normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("[^A-Z0-9]+")]
    private static partial Regex InvalidCharactersRegex();

    [GeneratedRegex("_{2,}")]
    private static partial Regex RepeatedSeparatorsRegex();
}
