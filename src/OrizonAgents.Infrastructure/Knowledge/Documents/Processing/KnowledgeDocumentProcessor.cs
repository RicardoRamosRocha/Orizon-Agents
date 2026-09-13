using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;
using OrizonAgents.Application.Knowledge.Embeddings;
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
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<KnowledgeDocumentProcessor> _logger;

    public KnowledgeDocumentProcessor(
        OrizonAgentsDbContext dbContext,
        IKnowledgeFileStorage storage,
        IEnumerable<IKnowledgeDocumentExtractor> extractors,
        IKnowledgeTextChunker chunker,
        IEmbeddingGenerator embeddingGenerator,
        ILogger<KnowledgeDocumentProcessor> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _extractors = extractors;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
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
                KnowledgeChunk knowledgeChunk = new(
                    document.TenantId,
                    document.Id,
                    chunk.Position,
                    chunk.Content);

                _dbContext.KnowledgeChunks.Add(knowledgeChunk);

                float[] embedding = await _embeddingGenerator.GenerateAsync(
                    knowledgeChunk.Content,
                    cancellationToken);

                _dbContext.KnowledgeChunkEmbeddings.Add(
                    new KnowledgeChunkEmbedding(
                        document.TenantId,
                        knowledgeChunk.Id,
                        _embeddingGenerator.Provider,
                        _embeddingGenerator.Model,
                        embedding));
            }

            document.MarkReady();

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return OperationResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Processamento do documento de conhecimento cancelado {DocumentId}.",
                document.Id);

            _dbContext.ChangeTracker.Clear();

            KnowledgeDocument? cancelledDocument =
                await _dbContext.KnowledgeDocuments
                    .SingleOrDefaultAsync(
                        candidate => candidate.Id == documentId,
                        CancellationToken.None);

            if (cancelledDocument is not null)
            {
                cancelledDocument.MarkFailed(
                    "Processamento do documento cancelado.");

                await _dbContext.SaveChangesAsync(
                    CancellationToken.None);
            }

            throw;
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
