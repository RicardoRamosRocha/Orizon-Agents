using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Requests;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Infrastructure.Knowledge;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Knowledge;

public sealed class KnowledgeServiceUploadTests
{
    [Fact]
    public async Task UploadDocumentAsync_ProcessesDocumentBeforeReturningSuccess()
    {
        Guid tenantId = Guid.NewGuid();
        var currentTenant = new CurrentTenant();
        currentTenant.SetTenantId(tenantId);

        await using var db = CreateDbContext(currentTenant);
        var knowledgeBase = new KnowledgeBase(tenantId, "Test knowledge base");
        db.KnowledgeBases.Add(knowledgeBase);
        await db.SaveChangesAsync();

        var processor = new RecordingProcessor();
        var service = new KnowledgeService(
            db,
            currentTenant,
            new StubStorage(),
            processor);

        OperationResult<Guid> result = await service.UploadDocumentAsync(
            new UploadKnowledgeDocumentRequest(
                knowledgeBase.Id,
                "manual.txt",
                "text/plain",
                12,
                new MemoryStream("knowledge text"u8.ToArray())));

        Assert.True(result.Succeeded, result.FirstError);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Equal(result.Value, processor.ProcessedDocumentId);
    }

    [Fact]
    public async Task UploadDocumentAsync_ReturnsProcessingFailureWithoutLosingDocument()
    {
        Guid tenantId = Guid.NewGuid();
        var currentTenant = new CurrentTenant();
        currentTenant.SetTenantId(tenantId);

        await using var db = CreateDbContext(currentTenant);
        var knowledgeBase = new KnowledgeBase(tenantId, "Test knowledge base");
        db.KnowledgeBases.Add(knowledgeBase);
        await db.SaveChangesAsync();

        var service = new KnowledgeService(
            db,
            currentTenant,
            new StubStorage(),
            new FailingProcessor());

        OperationResult<Guid> result = await service.UploadDocumentAsync(
            new UploadKnowledgeDocumentRequest(
                knowledgeBase.Id,
                "manual.txt",
                "text/plain",
                12,
                new MemoryStream("knowledge text"u8.ToArray())));

        Assert.False(result.Succeeded);
        Assert.Contains("embedding unavailable", result.Errors);
        Assert.Single(await db.KnowledgeDocuments.ToListAsync());
    }

    private static OrizonAgentsDbContext CreateDbContext(ICurrentTenant currentTenant)
    {
        DbContextOptions<OrizonAgentsDbContext> options =
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase($"KnowledgeService-{Guid.NewGuid():N}")
                .Options;

        return new OrizonAgentsDbContext(options, currentTenant);
    }

    private sealed class RecordingProcessor : IKnowledgeDocumentProcessor
    {
        public Guid ProcessedDocumentId { get; private set; }

        public Task<OperationResult> ProcessAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            ProcessedDocumentId = documentId;
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class FailingProcessor : IKnowledgeDocumentProcessor
    {
        public Task<OperationResult> ProcessAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Failure("embedding unavailable"));
    }

    private sealed class StubStorage : IKnowledgeFileStorage
    {
        public Task<string> SaveAsync(
            Guid tenantId,
            string fileName,
            Stream content,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"{tenantId:N}/{fileName}");

        public Task<Stream> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream("knowledge text"u8.ToArray()));

        public Task DeleteAsync(
            string storageKey,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
