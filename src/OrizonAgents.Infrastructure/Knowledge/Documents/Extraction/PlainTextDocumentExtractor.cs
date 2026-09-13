using System.Text;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

public sealed class PlainTextDocumentExtractor :
    IKnowledgeDocumentExtractor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".md",
            ".markdown"
        };

    public bool CanExtract(
        string fileName,
        string contentType)
    {
        string extension = Path.GetExtension(fileName);

        return SupportedExtensions.Contains(extension) ||
            contentType.Equals(
                "text/plain",
                StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals(
                "text/markdown",
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

        string text =
            await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "O documento não contém texto.");
        }

        return new KnowledgeDocumentContent(
            text.Trim(),
            contentType);
    }
}
