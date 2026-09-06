using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace OrizonAgents.Infrastructure.Integrations.Gmail;

public interface IGmailMessageContentReducer
{
    string? Reduce(string? content, bool isHtml);
}

public sealed partial class GmailMessageContentReducer
    : IGmailMessageContentReducer
{
    public const int MaximumBodyCharacters = 8_000;

    private const string ReductionMarker =
        "[Trecho intermediário reduzido para economizar contexto.]";

    public string? Reduce(string? content, bool isHtml)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        string text = isHtml
            ? ConvertHtmlToText(content)
            : content;

        text = NormalizeWhitespace(text);
        text = RemoveQuotedReply(text);
        text = RemoveEvidentSignature(text);
        text = NormalizeWhitespace(text);

        return string.IsNullOrWhiteSpace(text)
            ? null
            : LimitLength(text);
    }

    private static string ConvertHtmlToText(string html)
    {
        string text = ScriptAndStylePattern().Replace(html, string.Empty);
        text = BreakPattern().Replace(text, "\n");
        text = BlockEndPattern().Replace(text, "\n");
        text = TagPattern().Replace(text, " ");
        return WebUtility.HtmlDecode(text);
    }

    private static string NormalizeWhitespace(string content)
    {
        string normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace('\u00A0', ' ');

        string[] lines = normalized.Split('\n');
        var builder = new StringBuilder(normalized.Length);
        int emptyLineCount = 0;

        foreach (string sourceLine in lines)
        {
            string line = HorizontalWhitespacePattern()
                .Replace(sourceLine.Trim(), " ");

            if (line.Length == 0)
            {
                emptyLineCount++;
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(emptyLineCount > 0 ? "\n\n" : "\n");
            }

            builder.Append(line);
            emptyLineCount = 0;
        }

        return builder.ToString();
    }

    private static string RemoveQuotedReply(string content)
    {
        string[] lines = content.Split('\n');

        for (int index = 1; index < lines.Length; index++)
        {
            if (ReplyMarkerPattern().IsMatch(lines[index]))
            {
                return string.Join('\n', lines[..index]).TrimEnd();
            }
        }

        int quotedStart = lines.Length;
        int quotedLineCount = 0;

        for (int index = lines.Length - 1; index >= 0; index--)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                if (quotedLineCount > 0)
                {
                    quotedStart = index;
                }

                continue;
            }

            if (!QuotedLinePattern().IsMatch(lines[index]))
            {
                break;
            }

            quotedLineCount++;
            quotedStart = index;
        }

        return quotedLineCount >= 2 && quotedStart > 0
            ? string.Join('\n', lines[..quotedStart]).TrimEnd()
            : content;
    }

    private static string RemoveEvidentSignature(string content)
    {
        string[] lines = content.Split('\n');

        for (int index = lines.Length - 1; index > 0; index--)
        {
            if (!SignatureDelimiterPattern().IsMatch(lines[index]))
            {
                continue;
            }

            int nonEmptySuffixLines = lines[(index + 1)..]
                .Count(line => !string.IsNullOrWhiteSpace(line));

            if (nonEmptySuffixLines <= 6)
            {
                return string.Join('\n', lines[..index]).TrimEnd();
            }
        }

        if (lines.Length > 1 &&
            MobileSignaturePattern().IsMatch(lines[^1]))
        {
            return string.Join('\n', lines[..^1]).TrimEnd();
        }

        return content;
    }

    private static string LimitLength(string content)
    {
        if (content.Length <= MaximumBodyCharacters)
        {
            return content;
        }

        int markerLength = ReductionMarker.Length + 4;
        int availableCharacters = MaximumBodyCharacters - markerLength;
        int headTarget = availableCharacters * 4 / 5;
        int tailTarget = availableCharacters - headTarget;

        int headEnd = FindPreviousBoundary(content, headTarget);
        int tailStart = FindNextBoundary(
            content,
            content.Length - tailTarget);

        if (headEnd <= 0)
        {
            headEnd = headTarget;
        }

        if (tailStart <= headEnd || tailStart >= content.Length)
        {
            tailStart = content.Length - tailTarget;
        }

        return content[..headEnd].TrimEnd() +
            "\n\n" + ReductionMarker + "\n\n" +
            content[tailStart..].TrimStart();
    }

    private static int FindPreviousBoundary(string content, int start)
    {
        int paragraph = content.LastIndexOf("\n\n", start, StringComparison.Ordinal);
        if (paragraph >= start / 2)
        {
            return paragraph;
        }

        return content.LastIndexOf('\n', start);
    }

    private static int FindNextBoundary(string content, int start)
    {
        int paragraph = content.IndexOf("\n\n", start, StringComparison.Ordinal);
        if (paragraph >= 0)
        {
            return paragraph + 2;
        }

        int line = content.IndexOf('\n', start);
        return line >= 0 ? line + 1 : -1;
    }

    [GeneratedRegex(@"(?is)<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.CultureInvariant, 250)]
    private static partial Regex ScriptAndStylePattern();

    [GeneratedRegex(@"(?i)<br\s*/?>", RegexOptions.CultureInvariant, 250)]
    private static partial Regex BreakPattern();

    [GeneratedRegex(@"(?i)</(?:p|div|li|tr|h[1-6]|blockquote)\s*>", RegexOptions.CultureInvariant, 250)]
    private static partial Regex BlockEndPattern();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant, 250)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"[\t\f\v ]{2,}", RegexOptions.CultureInvariant, 250)]
    private static partial Regex HorizontalWhitespacePattern();

    [GeneratedRegex(@"(?i)^\s*(?:on\s+.+\s+wrote:|em\s+.+\s+escreveu:|-{2,}\s*(?:original message|mensagem original|forwarded message|mensagem encaminhada)\s*-{2,})\s*$", RegexOptions.CultureInvariant, 250)]
    private static partial Regex ReplyMarkerPattern();

    [GeneratedRegex(@"^\s*>\s?.+$", RegexOptions.CultureInvariant, 250)]
    private static partial Regex QuotedLinePattern();

    [GeneratedRegex(@"^--\s*$", RegexOptions.CultureInvariant, 250)]
    private static partial Regex SignatureDelimiterPattern();

    [GeneratedRegex(@"(?i)^\s*(?:sent from my|enviado do meu|enviado de meu)\s+.+$", RegexOptions.CultureInvariant, 250)]
    private static partial Regex MobileSignaturePattern();
}
