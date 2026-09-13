using System.Text;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

public sealed class CsvDocumentExtractor :
    IKnowledgeDocumentExtractor
{
    private static readonly char[] SupportedDelimiters =
    {
        ';',
        ',',
        '\t'
    };

    public bool CanExtract(
        string fileName,
        string contentType)
    {
        string extension = Path.GetExtension(fileName);

        return extension.Equals(
                ".csv",
                StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals(
                "text/csv",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<KnowledgeDocumentContent> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        string raw =
            await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "A planilha CSV não contém dados.");
        }

        string[] lines = raw
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            throw new InvalidOperationException(
                "A planilha CSV não contém dados.");
        }

        char delimiter = DetectDelimiter(lines[0]);

        string[] headers = ParseLine(
            lines[0],
            delimiter);

        if (headers.Length == 0)
        {
            throw new InvalidOperationException(
                "Não foi possível identificar as colunas do CSV.");
        }

        var text = new StringBuilder();

        text.AppendLine($"Tabela: {Path.GetFileName(fileName)}");
        text.AppendLine();
        text.AppendLine(
            $"Colunas: {string.Join(" | ", headers)}");

        for (int index = 1; index < lines.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] values = ParseLine(
                lines[index],
                delimiter);

            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            text.AppendLine();
            text.AppendLine($"Linha {index}:");

            for (int column = 0; column < headers.Length; column++)
            {
                string header =
                    string.IsNullOrWhiteSpace(headers[column])
                        ? $"Coluna {column + 1}"
                        : headers[column].Trim();

                string value =
                    column < values.Length
                        ? values[column].Trim()
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                text.AppendLine($"{header}: {value}");
            }
        }

        return new KnowledgeDocumentContent(
            text.ToString().Trim(),
            contentType);
    }

    private static char DetectDelimiter(
        string header)
    {
        return SupportedDelimiters
            .OrderByDescending(
                delimiter => header.Count(
                    character => character == delimiter))
            .First();
    }

    private static string[] ParseLine(
        string line,
        char delimiter)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        bool insideQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < line.Length &&
                    line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                insideQuotes = !insideQuotes;
                continue;
            }

            if (character == delimiter && !insideQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());

        return values.ToArray();
    }
}
