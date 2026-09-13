using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge.Embeddings;
using OrizonAgents.Application.Knowledge.Retrieval;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Infrastructure.Persistence;
using Pgvector;

namespace OrizonAgents.Infrastructure.Knowledge.Retrieval;

public sealed class SemanticKnowledgeRetriever : ISemanticKnowledgeRetriever
{
    private const int MaxSafeResults = 10;
    private const int ExpectedDimensions = 1536;

    private readonly OrizonAgentsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmbeddingGenerator _embeddingGenerator;

    public SemanticKnowledgeRetriever(
        OrizonAgentsDbContext dbContext,
        ICurrentTenant currentTenant,
        IEmbeddingGenerator embeddingGenerator)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<IReadOnlyList<KnowledgeRetrievalResult>> RetrieveAsync(
        Guid agentId,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty ||
            string.IsNullOrWhiteSpace(query) ||
            maxResults <= 0 ||
            !_currentTenant.HasTenant)
        {
            return Array.Empty<KnowledgeRetrievalResult>();
        }

        int safeMaxResults = Math.Min(maxResults, MaxSafeResults);
        float[] queryEmbedding = await _embeddingGenerator.GenerateAsync(
            query,
            cancellationToken);

        if (queryEmbedding.Length != ExpectedDimensions ||
            _embeddingGenerator.Dimensions != ExpectedDimensions)
        {
            throw new InvalidOperationException(
                $"O embedding da consulta deve possuir {ExpectedDimensions} dimensões.");
        }

        const string sql = """
            SELECT
                kb."Id" AS "KnowledgeBaseId",
                kb."Name" AS "KnowledgeBaseName",
                d."Id" AS "DocumentId",
                d."FileName" AS "DocumentName",
                c."Position" AS "ChunkPosition",
                c."Id" AS "KnowledgeChunkId",
                c."Content" AS "Content",
                1 - (e."Embedding" <=> @queryEmbedding) AS "SemanticScore"
            FROM "AgentKnowledgeBindings" b
            INNER JOIN "KnowledgeBases" kb
                ON kb."Id" = b."KnowledgeBaseId"
                AND kb."TenantId" = b."TenantId"
            INNER JOIN "KnowledgeDocuments" d
                ON d."KnowledgeBaseId" = kb."Id"
                AND d."TenantId" = b."TenantId"
            INNER JOIN "KnowledgeChunks" c
                ON c."DocumentId" = d."Id"
                AND c."TenantId" = b."TenantId"
            INNER JOIN "KnowledgeChunkEmbeddings" e
                ON e."KnowledgeChunkId" = c."Id"
                AND e."TenantId" = b."TenantId"
                AND e."Provider" = @provider
                AND e."Model" = @model
                AND e."Dimensions" = @dimensions
            WHERE b."TenantId" = @tenantId
              AND b."AgentId" = @agentId
              AND kb."IsActive" = TRUE
              AND d."Status" = @readyStatus
            ORDER BY e."Embedding" <=> @queryEmbedding ASC,
                     d."FileName" ASC,
                     c."Position" ASC
            LIMIT @maxResults;
            """;

        DbConnection connection = _dbContext.Database.GetDbConnection();
        bool shouldCloseConnection = connection.State == ConnectionState.Closed;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            AddParameter(command, "tenantId", _currentTenant.TenantId!.Value);
            AddParameter(command, "agentId", agentId);
            AddParameter(command, "provider", _embeddingGenerator.Provider);
            AddParameter(command, "model", _embeddingGenerator.Model);
            AddParameter(command, "dimensions", ExpectedDimensions);
            AddParameter(command, "readyStatus", KnowledgeDocumentStatus.Ready.ToString());
            AddParameter(command, "maxResults", safeMaxResults);
            AddParameter(command, "queryEmbedding", new Vector(queryEmbedding));

            await using DbDataReader reader = await command.ExecuteReaderAsync(
                cancellationToken);
            var results = new List<KnowledgeRetrievalResult>(safeMaxResults);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(
                    new KnowledgeRetrievalResult(
                        reader.GetGuid(reader.GetOrdinal("KnowledgeBaseId")),
                        reader.GetString(reader.GetOrdinal("KnowledgeBaseName")),
                        reader.GetGuid(reader.GetOrdinal("DocumentId")),
                        reader.GetString(reader.GetOrdinal("DocumentName")),
                        reader.GetInt32(reader.GetOrdinal("ChunkPosition")),
                        reader.GetString(reader.GetOrdinal("Content")),
                        reader.GetDouble(reader.GetOrdinal("SemanticScore")),
                        KnowledgeChunkId: reader.GetGuid(reader.GetOrdinal("KnowledgeChunkId"))));
            }

            return results;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
