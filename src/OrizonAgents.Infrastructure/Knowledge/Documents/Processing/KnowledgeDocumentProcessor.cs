using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Knowledge.Documents.Processing;

public sealed class KnowledgeDocumentProcessor :
    IKnowledgeDocumentProcessor
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IKnowledgeFileStorage _storage;
    private readonly IEnumerable<IKnowledgeDocumentExtractor> _extractors;
    private readonly IKnowledgeTextChunker _chunker;
    private readonly ILogger<KnowledgeDocumentProcessor> _logger;

    public KnowledgeDocumentProcessor(
        OrizonAgentsDbContext dbContext,
        IKnowledgeFileStorage storage,
        IEnumerable<IKnowledgeDocumentExtractor> extractors,
        IKnowledgeTextChunker chunker,
        ILogger<KnowledgeDocumentProcessor> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _extractors = extractors;
        _chunker = chunker;
        _logger = logger;
    }

    public async Task<OperationResult> ProcessAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        KnowledgeDocument? document =
            await _dbContext.KnowledgeDocuments
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == documentId,
                    cancellationToken);

        if (document is null)
        {
            return OperationResult.Failure(
                "Documento de conhecimento não encontrado.");
        }

        IKnowledgeDocumentExtractor? extractor =
            _extractors.FirstOrDefault(
                candidate => candidate.CanExtract(
                    document.FileName,
                    document.ContentType));

        if (extractor is null)
        {
            document.MarkFailed(
                "Formato de documento ainda não suportado.");

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return OperationResult.Failure(
                "Formato de documento ainda não suportado.");
        }

        document.MarkProcessing();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        try
        {
            await using Stream stream =
                await _storage.OpenReadAsync(
                    document.StorageKey,
                    cancellationToken);

            KnowledgeDocumentContent extracted =
                await extractor.ExtractAsync(
                    document.FileName,
                    document.ContentType,
                    stream,
                    cancellationToken);

            IReadOnlyList<KnowledgeDocumentChunk> chunks =
                _chunker.Chunk(extracted.Text);

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException(
                    "Nenhum conteúdo útil foi encontrado no documento.");
            }

            List<KnowledgeChunk> existingChunks =
                await _dbContext.KnowledgeChunks
                    .Where(chunk =>
                        chunk.DocumentId == document.Id)
                    .ToListAsync(cancellationToken);

            if (existingChunks.Count > 0)
            {
                _dbContext.KnowledgeChunks.RemoveRange(
                    existingChunks);
            }

            foreach (KnowledgeDocumentChunk chunk in chunks)
            {
                _dbContext.KnowledgeChunks.Add(
                    new KnowledgeChunk(
                        document.TenantId,
                        document.Id,
                        chunk.Position,
                        chunk.Content));
            }

            document.MarkReady();

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha ao processar documento de conhecimento {DocumentId}.",
                document.Id);

            _dbContext.ChangeTracker.Clear();

            KnowledgeDocument? failedDocument =
                await _dbContext.KnowledgeDocuments
                    .SingleOrDefaultAsync(
                        candidate =>
                            candidate.Id == documentId,
                        cancellationToken);

            if (failedDocument is not null)
            {
                failedDocument.MarkFailed(
                    LimitError(exception.Message));

                await _dbContext.SaveChangesAsync(
                    cancellationToken);
            }

            return OperationResult.Failure(
                "Não foi possível processar o documento.");
        }
    }

    private static string LimitError(string error)
    {
        const int maxLength = 4000;

        if (string.IsNullOrWhiteSpace(error))
        {
            return "Document processing failed.";
        }

        string normalized = error.Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
