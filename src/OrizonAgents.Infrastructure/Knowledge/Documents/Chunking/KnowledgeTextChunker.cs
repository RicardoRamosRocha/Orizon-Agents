using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Chunking;

public sealed class KnowledgeTextChunker :
    IKnowledgeTextChunker
{
    private const int MaxChunkLength = 1800;
    private const int OverlapLength = 200;

    public IReadOnlyList<KnowledgeDocumentChunk> Chunk(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<KnowledgeDocumentChunk>();
        }

        string normalized = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Trim();

        var chunks = new List<KnowledgeDocumentChunk>();

        int start = 0;
        int position = 0;

        while (start < normalized.Length)
        {
            int remaining = normalized.Length - start;
            int length = Math.Min(MaxChunkLength, remaining);

            if (length == MaxChunkLength)
            {
                int preferredBreak = FindPreferredBreak(
                    normalized,
                    start,
                    length);

                if (preferredBreak > 0)
                {
                    length = preferredBreak;
                }
            }

            string content = normalized
                .Substring(start, length)
                .Trim();

            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(
                    new KnowledgeDocumentChunk(
                        position++,
                        content));
            }

            if (start + length >= normalized.Length)
            {
                break;
            }

            int advance = Math.Max(
                1,
                length - OverlapLength);

            start += advance;
        }

        return chunks;
    }

    private static int FindPreferredBreak(
        string text,
        int start,
        int length)
    {
        int minimumBreak =
            Math.Max(1, length / 2);

        string window =
            text.Substring(start, length);

        int paragraph =
            window.LastIndexOf(
                "\n\n",
                StringComparison.Ordinal);

        if (paragraph >= minimumBreak)
        {
            return paragraph + 2;
        }

        int line =
            window.LastIndexOf('\n');

        if (line >= minimumBreak)
        {
            return line + 1;
        }

        int sentence =
            window.LastIndexOf(". ", StringComparison.Ordinal);

        if (sentence >= minimumBreak)
        {
            return sentence + 2;
        }

        int space =
            window.LastIndexOf(' ');

        return space >= minimumBreak
            ? space + 1
            : length;
    }
}
