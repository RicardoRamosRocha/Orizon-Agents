using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OrizonAgents.Domain.Tenants;

public static partial class TenantSlug
{
    public const int MaxLength = 100;

    public static string Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = RemoveDiacritics(value.Trim().ToLowerInvariant());
        normalized = InvalidSlugCharactersRegex().Replace(normalized, "-");
        normalized = RepeatedSeparatorsRegex().Replace(normalized, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Slug must contain at least one letter or number.", nameof(value));
        }

        return normalized.Length <= MaxLength
            ? normalized
            : normalized[..MaxLength].Trim('-');
    }

    public static void EnsureValid(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (slug.Length > MaxLength || !ValidSlugRegex().IsMatch(slug))
        {
            throw new ArgumentException("Slug must contain only lowercase letters, numbers and hyphens.", nameof(slug));
        }
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

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex InvalidSlugCharactersRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedSeparatorsRegex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex ValidSlugRegex();
}
