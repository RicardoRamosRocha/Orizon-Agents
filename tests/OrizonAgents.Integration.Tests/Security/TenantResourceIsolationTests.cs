using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Knowledge.Documents;
using OrizonAgents.Application.Knowledge.Documents.Models;
using OrizonAgents.Application.Knowledge.Retrieval.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Knowledge;
using OrizonAgents.Infrastructure.Agents.Execution;
using OrizonAgents.Infrastructure.Knowledge;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Security;

public sealed class TenantResourceIsolationTests
{
    [Fact]
    public async Task TenantA_CannotReadTenantBConversation()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        var currentTenant = new CurrentTenant();
        currentTenant.SetTenantId(tenantA);
        await using OrizonAgentsDbContext db = CreateDbContext(currentTenant);

        var agentA = new AiAgent(tenantA, "Agente A", "Sistema", AiProvider.Groq, "model");
        var agentB = new AiAgent(tenantB, "Agente B", "Sistema", AiProvider.Groq, "model");
        var conversationB = new AiConversation(tenantB, agentB.Id, "Conversa B");
        conversationB.AddUserMessage("Segredo do tenant B");
        db.AddRange(agentA, agentB, conversationB);
        await db.SaveChangesAsync();

        var service = new AiConversationService(db);

        Assert.Null(await service.GetAsync(conversationB.Id, agentB.Id));
        Assert.Empty(await service.ListAsync(agentB.Id));
    }

    [Fact]
    public async Task TenantA_CannotReadTenantBKnowledgeBase()
    {
        Guid tenantA = Guid.NewGuid();
        Guid tenantB = Guid.NewGuid();
        var currentTenant = new CurrentTenant();
        currentTenant.SetTenantId(tenantA);
        await using OrizonAgentsDbContext db = CreateDbContext(currentTenant);

        var knowledgeBaseA = new KnowledgeBase(tenantA, "Base A");
        var knowledgeBaseB = new KnowledgeBase(tenantB, "Base B");
        var documentB = new KnowledgeDocument(
            tenantB,
            knowledgeBaseB.Id,
            "tenant-b.txt",
            "text/plain",
            10,
            "tenant-b/tenant-b.txt");
        db.AddRange(knowledgeBaseA, knowledgeBaseB, documentB);
        await db.SaveChangesAsync();

        var service = new KnowledgeService(
            db,
            currentTenant,
            new StubStorage(),
            new StubProcessor());

        Assert.Null(await service.GetAsync(knowledgeBaseB.Id));
        Assert.Single(await service.ListAsync());
        Assert.Equal(knowledgeBaseA.Id, (await service.ListAsync())[0].Id);
    }

    private static OrizonAgentsDbContext CreateDbContext(ICurrentTenant currentTenant)
    {
        DbContextOptions<OrizonAgentsDbContext> options =
            new DbContextOptionsBuilder<OrizonAgentsDbContext>()
                .UseInMemoryDatabase($"TenantIsolation-{Guid.NewGuid():N}")
                .Options;

        return new OrizonAgentsDbContext(options, currentTenant);
    }

    private sealed class StubStorage : IKnowledgeFileStorage
    {
        public Task<string> SaveAsync(Guid tenantId, string fileName, Stream content, CancellationToken cancellationToken = default) =>
            Task.FromResult("unused");

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubProcessor : IKnowledgeDocumentProcessor
    {
        public Task<OrizonAgents.Application.Common.Results.OperationResult> ProcessAsync(
            Guid documentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OrizonAgents.Application.Common.Results.OperationResult.Success());
    }
}
