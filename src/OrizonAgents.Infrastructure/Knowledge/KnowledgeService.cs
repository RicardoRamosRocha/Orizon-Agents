using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Models;
using OrizonAgents.Application.Knowledge.Requests;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Knowledge;

public sealed class KnowledgeService : IKnowledgeService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".md",
            ".markdown",
            ".pdf",
            ".csv",
            ".xlsx",
            ".docx"
        };

    private readonly OrizonAgentsDbContext _dbContext;
    private readonly ICurrentTenant _currentTenant;
    private readonly IKnowledgeFileStorage _storage;
    private readonly IKnowledgeDocumentProcessor _processor;

    public KnowledgeService(
        OrizonAgentsDbContext dbContext,
        ICurrentTenant currentTenant,
        IKnowledgeFileStorage storage,
        IKnowledgeDocumentProcessor processor)
    {
        _dbContext = dbContext;
        _currentTenant = currentTenant;
        _storage = storage;
        _processor = processor;
    }

    public async Task<IReadOnlyList<KnowledgeBaseListItemDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeBases
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new KnowledgeBaseListItemDto(
                x.Id,
                x.Name,
                x.Description,
                x.IsActive,
                x.Documents.Count))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<KnowledgeBaseDetailsDto?> GetAsync(
        Guid knowledgeBaseId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.KnowledgeBases
            .AsNoTracking()
            .Where(x => x.Id == knowledgeBaseId)
            .Select(x => new KnowledgeBaseDetailsDto(
                x.Id,
                x.Name,
                x.Description,
                x.IsActive,
                x.Documents
                    .OrderByDescending(document => document.CreatedAtUtc)
                    .Select(document => new KnowledgeDocumentDto(
                        document.Id,
                        document.FileName,
                        document.ContentType,
                        document.SizeBytes,
                        document.Status,
                        document.ProcessingError,
                        document.Chunks.Count,
                        document.CreatedAtUtc))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<OperationResult<Guid>> CreateAsync(
        CreateKnowledgeBaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            return OperationResult<Guid>.Failure(
                "Tenant atual não está definido.");
        }

        try
        {
            var knowledgeBase = new KnowledgeBase(
                _currentTenant.TenantId.Value,
                request.Name,
                request.Description);

            _dbContext.KnowledgeBases.Add(knowledgeBase);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return OperationResult<Guid>.Success(
                knowledgeBase.Id);
        }
        catch (ArgumentException exception)
        {
            return OperationResult<Guid>.Failure(
                exception.Message);
        }
    }

    public async Task<OperationResult<Guid>> UploadDocumentAsync(
        UploadKnowledgeDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            return OperationResult<Guid>.Failure(
                "Tenant atual não está definido.");
        }

        if (request.KnowledgeBaseId == Guid.Empty)
        {
            return OperationResult<Guid>.Failure(
                "Base de conhecimento é obrigatória.");
        }

        if (request.SizeBytes <= 0)
        {
            return OperationResult<Guid>.Failure(
                "O arquivo está vazio.");
        }

        if (request.SizeBytes > MaxFileSizeBytes)
        {
            return OperationResult<Guid>.Failure(
                "O arquivo excede o limite de 10 MB.");
        }

        string extension =
            Path.GetExtension(request.FileName);

        if (!SupportedExtensions.Contains(extension))
        {
            return OperationResult<Guid>.Failure(
                "Formato não suportado. Utilize TXT, Markdown, PDF, CSV, XLSX ou DOCX.");
        }

        KnowledgeBase? knowledgeBase =
            await _dbContext.KnowledgeBases
                .SingleOrDefaultAsync(
                    x => x.Id == request.KnowledgeBaseId,
                    cancellationToken);

        if (knowledgeBase is null)
        {
            return OperationResult<Guid>.Failure(
                "Base de conhecimento não encontrada.");
        }

        if (!knowledgeBase.IsActive)
        {
            return OperationResult<Guid>.Failure(
                "A base de conhecimento está desativada.");
        }

        string? storageKey = null;

        try
        {
            storageKey = await _storage.SaveAsync(
                _currentTenant.TenantId.Value,
                request.FileName,
                request.Content,
                cancellationToken);

            var document = new KnowledgeDocument(
                _currentTenant.TenantId.Value,
                knowledgeBase.Id,
                request.FileName,
                NormalizeContentType(
                    request.ContentType,
                    extension),
                request.SizeBytes,
                storageKey);

            _dbContext.KnowledgeDocuments.Add(document);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return OperationResult<Guid>.Success(
                document.Id);
        }
        catch (Exception)
        {
            if (!string.IsNullOrWhiteSpace(storageKey))
            {
                await _storage.DeleteAsync(
                    storageKey,
                    cancellationToken);
            }

            throw;
        }
    }

    public Task<OperationResult> ProcessDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return _processor.ProcessAsync(
            documentId,
            cancellationToken);
    }

    public async Task<OperationResult> DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        KnowledgeDocument? document =
            await _dbContext.KnowledgeDocuments
                .SingleOrDefaultAsync(
                    x => x.Id == documentId,
                    cancellationToken);

        if (document is null)
        {
            return OperationResult.Failure(
                "Documento não encontrado.");
        }

        string storageKey = document.StorageKey;

        _dbContext.KnowledgeDocuments.Remove(document);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _storage.DeleteAsync(
            storageKey,
            cancellationToken);

        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<AgentKnowledgeBindingDto>> ListForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            return Array.Empty<AgentKnowledgeBindingDto>();
        }

        bool agentExists =
            await _dbContext.AiAgents
                .AsNoTracking()
                .AnyAsync(
                    agent => agent.Id == agentId,
                    cancellationToken);

        if (!agentExists)
        {
            return Array.Empty<AgentKnowledgeBindingDto>();
        }

        return await _dbContext.KnowledgeBases
            .AsNoTracking()
            .OrderBy(knowledgeBase => knowledgeBase.Name)
            .Select(knowledgeBase =>
                new AgentKnowledgeBindingDto(
                    knowledgeBase.Id,
                    knowledgeBase.Name,
                    knowledgeBase.Description,
                    knowledgeBase.IsActive,
                    knowledgeBase.Documents.Count,
                    knowledgeBase.AgentBindings.Any(
                        binding => binding.AgentId == agentId)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OperationResult> BindToAgentAsync(
        Guid agentId,
        Guid knowledgeBaseId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentTenant.TenantId.HasValue)
        {
            return OperationResult.Failure(
                "Tenant atual não está definido.");
        }

        bool agentExists =
            await _dbContext.AiAgents
                .AsNoTracking()
                .AnyAsync(
                    agent => agent.Id == agentId,
                    cancellationToken);

        if (!agentExists)
        {
            return OperationResult.Failure(
                "Agente não encontrado.");
        }

        KnowledgeBase? knowledgeBase =
            await _dbContext.KnowledgeBases
                .SingleOrDefaultAsync(
                    item => item.Id == knowledgeBaseId,
                    cancellationToken);

        if (knowledgeBase is null)
        {
            return OperationResult.Failure(
                "Base de conhecimento não encontrada.");
        }

        if (!knowledgeBase.IsActive)
        {
            return OperationResult.Failure(
                "A base de conhecimento está desativada.");
        }

        bool alreadyBound =
            await _dbContext.AgentKnowledgeBindings
                .AsNoTracking()
                .AnyAsync(
                    binding =>
                        binding.AgentId == agentId &&
                        binding.KnowledgeBaseId == knowledgeBaseId,
                    cancellationToken);

        if (alreadyBound)
        {
            return OperationResult.Success();
        }

        _dbContext.AgentKnowledgeBindings.Add(
            new AgentKnowledgeBinding(
                _currentTenant.TenantId.Value,
                agentId,
                knowledgeBaseId));

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> UnbindFromAgentAsync(
        Guid agentId,
        Guid knowledgeBaseId,
        CancellationToken cancellationToken = default)
    {
        AgentKnowledgeBinding? binding =
            await _dbContext.AgentKnowledgeBindings
                .SingleOrDefaultAsync(
                    item =>
                        item.AgentId == agentId &&
                        item.KnowledgeBaseId == knowledgeBaseId,
                    cancellationToken);

        if (binding is null)
        {
            return OperationResult.Success();
        }

        _dbContext.AgentKnowledgeBindings.Remove(binding);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return OperationResult.Success();
    }

    private static string NormalizeContentType(
        string contentType,
        string extension)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            !contentType.Equals(
                "application/octet-stream",
                StringComparison.OrdinalIgnoreCase))
        {
            return contentType.Trim();
        }

        if (extension.Equals(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            return "application/pdf";
        }

        return extension.Equals(
            ".md",
            StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(
                ".markdown",
                StringComparison.OrdinalIgnoreCase)
            ? "text/markdown"
            : "text/plain";
    }
}
