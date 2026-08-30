using System.Text;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

public sealed class PdfDocumentExtractor :
    IKnowledgeDocumentExtractor
{
    public bool CanExtract(
        string fileName,
        string contentType)
    {
        string extension = Path.GetExtension(fileName);

        return extension.Equals(
                ".pdf",
                StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals(
                "application/pdf",
                StringComparison.OrdinalIgnoreCase);
    }

    public Task<KnowledgeDocumentContent> ExtractAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = PdfDocument.Open(content);
        var text = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string pageText =
                ContentOrderTextExtractor.GetText(page);

            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.AppendLine();
                text.AppendLine();
            }

            text.Append(pageText.Trim());
        }

        string extractedText = text.ToString().Trim();

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException(
                "O PDF não contém texto extraível.");
        }

        return Task.FromResult(
            new KnowledgeDocumentContent(
                extractedText,
                contentType));
    }
}
