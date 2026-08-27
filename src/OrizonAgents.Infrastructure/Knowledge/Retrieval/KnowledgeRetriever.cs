using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Knowledge.Retrieval;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Knowledge.Retrieval;

public sealed class KnowledgeRetriever : IKnowledgeRetriever
{
    private readonly OrizonAgentsDbContext _dbContext;

    public KnowledgeRetriever(
        OrizonAgentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
        Guid agentId,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(query) ||
            maxResults <= 0)
        {
            return Array.Empty<KnowledgeRetrievalResult>();
        }

        string[] terms = ExtractTerms(query);

        if (terms.Length == 0)
        {
            return Array.Empty<KnowledgeRetrievalResult>();
        }

        var candidates =
            await (
                from binding in _dbContext.AgentKnowledgeBindings
                    .AsNoTracking()

                join knowledgeBase in _dbContext.KnowledgeBases
                    .AsNoTracking()
                    on binding.KnowledgeBaseId equals knowledgeBase.Id

                join document in _dbContext.KnowledgeDocuments
                    .AsNoTracking()
                    on knowledgeBase.Id equals document.KnowledgeBaseId

                join chunk in _dbContext.KnowledgeChunks
                    .AsNoTracking()
                    on document.Id equals chunk.DocumentId

                where binding.AgentId == agentId
                    && knowledgeBase.IsActive
                    && document.Status == KnowledgeDocumentStatus.Ready

                select new
                {
                    KnowledgeBaseId = knowledgeBase.Id,
                    KnowledgeBaseName = knowledgeBase.Name,
                    DocumentId = document.Id,
                    DocumentName = document.FileName,
                    ChunkPosition = chunk.Position,
                    chunk.Content
                })
            .ToListAsync(cancellationToken);

        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = Score(candidate.Content, terms)
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Candidate.DocumentName)
            .ThenBy(candidate => candidate.Candidate.ChunkPosition)
            .Take(Math.Min(maxResults, 10))
            .Select(candidate =>
                new KnowledgeRetrievalResult(
                    candidate.Candidate.KnowledgeBaseId,
                    candidate.Candidate.KnowledgeBaseName,
                    candidate.Candidate.DocumentId,
                    candidate.Candidate.DocumentName,
                    candidate.Candidate.ChunkPosition,
                    candidate.Candidate.Content))
            .ToArray();
    }

    private static string[] ExtractTerms(string query)
    {
        return Regex.Matches(
                query.ToLowerInvariant(),
                @"[\p{L}\p{N}][\p{L}\p{N}\-_]{2,}")
            .Select(match => match.Value)
            .Where(term => !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }

    private static int Score(
        string content,
        IReadOnlyList<string> terms)
    {
        string normalized =
            content.ToLowerInvariant();

        int score = 0;

        foreach (string term in terms)
        {
            int index = 0;

            while ((index = normalized.IndexOf(
                       term,
                       index,
                       StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                score++;
                index += term.Length;
            }
        }

        return score;
    }

    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "que",
            "qual",
            "quais",
            "como",
            "para",
            "por",
            "com",
            "uma",
            "uns",
            "das",
            "dos",
            "the",
            "and",
            "what",
            "which",
            "with",
            "from"
        };
}
