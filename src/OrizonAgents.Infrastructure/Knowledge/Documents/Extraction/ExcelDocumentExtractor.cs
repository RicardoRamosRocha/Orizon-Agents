using System.Text;
using ClosedXML.Excel;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

public sealed class ExcelDocumentExtractor :
    IKnowledgeDocumentExtractor
{
    public bool CanExtract(
        string fileName,
        string contentType)
    {
        string extension = Path.GetExtension(fileName);

        return extension.Equals(
                ".xlsx",
                StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                StringComparison.OrdinalIgnoreCase);
    }

    public Task<KnowledgeDocumentContent> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(content);
        var text = new StringBuilder();

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IXLRange? usedRange = worksheet.RangeUsed();

            if (usedRange is null)
            {
                continue;
            }

            IXLRangeRow firstRow = usedRange.FirstRow();

            string[] headers = firstRow
                .Cells()
                .Select(
                    (cell, index) =>
                    {
                        string value = cell.GetFormattedString().Trim();

                        return string.IsNullOrWhiteSpace(value)
                            ? $"Coluna {index + 1}"
                            : value;
                    })
                .ToArray();

            if (text.Length > 0)
            {
                text.AppendLine();
                text.AppendLine();
            }

            text.AppendLine($"Planilha: {worksheet.Name}");
            text.AppendLine($"Arquivo: {Path.GetFileName(fileName)}");
            text.AppendLine();
            text.AppendLine(
                $"Colunas: {string.Join(" | ", headers)}");

            int semanticRowNumber = 0;

            foreach (IXLRangeRow row in usedRange.RowsUsed().Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string[] values = row
                    .Cells(1, headers.Length)
                    .Select(cell => cell.GetFormattedString().Trim())
                    .ToArray();

                if (values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                semanticRowNumber++;

                text.AppendLine();
                text.AppendLine($"Linha {semanticRowNumber}:");

                for (int column = 0; column < headers.Length; column++)
                {
                    string value = values[column];

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    text.AppendLine(
                        $"{headers[column]}: {value}");
                }
            }
        }

        string extractedText = text.ToString().Trim();

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException(
                "A planilha Excel não contém dados.");
        }

        return Task.FromResult(
            new KnowledgeDocumentContent(
                extractedText,
                contentType));
    }
}
