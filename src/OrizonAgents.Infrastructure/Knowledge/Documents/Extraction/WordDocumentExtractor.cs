using DocumentFormat.OpenXml;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

public sealed class WordDocumentExtractor :
    IKnowledgeDocumentExtractor
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public bool CanExtract(
        string fileName,
        string contentType)
    {
        string extension = Path.GetExtension(fileName);

        return extension.Equals(
                ".docx",
                StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals(
                DocxContentType,
                StringComparison.OrdinalIgnoreCase);
    }

    public Task<KnowledgeDocumentContent> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using WordprocessingDocument document =
            WordprocessingDocument.Open(
                content,
                false);

        Body? body =
            document.MainDocumentPart?
                .Document
                .Body;

        if (body is null)
        {
            throw new InvalidOperationException(
                "O documento Word não contém conteúdo.");
        }

        var text = new StringBuilder();

        text.AppendLine(
            $"Documento: {Path.GetFileName(fileName)}");

        foreach (OpenXmlElement element in body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (element)
            {
                case Paragraph paragraph:
                    AppendParagraph(
                        text,
                        paragraph);
                    break;

                case Table table:
                    AppendTable(
                        text,
                        table,
                        cancellationToken);
                    break;
            }
        }

        string extractedText =
            text.ToString().Trim();

        string documentHeader =
            $"Documento: {Path.GetFileName(fileName)}";

        if (extractedText.Equals(
            documentHeader,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "O documento Word não contém texto extraível.");
        }

        return Task.FromResult(
            new KnowledgeDocumentContent(
                extractedText,
                contentType));
    }

    private static void AppendParagraph(
        StringBuilder text,
        Paragraph paragraph)
    {
        string value =
            paragraph.InnerText.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(value);
    }

    private static void AppendTable(
        StringBuilder text,
        Table table,
        CancellationToken cancellationToken)
    {
        TableRow[] rows =
            table.Elements<TableRow>()
                .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        string[] headers =
            GetCellValues(rows[0]);

        if (headers.Length == 0)
        {
            return;
        }

        for (int index = 0; index < headers.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(headers[index]))
            {
                headers[index] =
                    $"Coluna {index + 1}";
            }
        }

        text.AppendLine();
        text.AppendLine("Tabela:");
        text.AppendLine(
            $"Colunas: {string.Join(" | ", headers)}");

        int semanticRowNumber = 0;

        foreach (TableRow row in rows.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] values =
                GetCellValues(row);

            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            semanticRowNumber++;

            text.AppendLine();
            text.AppendLine(
                $"Linha {semanticRowNumber}:");

            for (int column = 0;
                 column < headers.Length;
                 column++)
            {
                string value =
                    column < values.Length
                        ? values[column]
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                text.AppendLine(
                    $"{headers[column]}: {value}");
            }
        }
    }

    private static string[] GetCellValues(
        TableRow row)
    {
        return row
            .Elements<TableCell>()
            .Select(cell => cell.InnerText.Trim())
            .ToArray();
    }
}
