using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.WhatsApp.Requests;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Domain.WhatsApp;
using OrizonAgents.Infrastructure.Billing;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Infrastructure.WhatsApp;
using OrizonAgents.Application.Common.Tenancy;

namespace OrizonAgents.Integration.Tests.WhatsApp;

public sealed class WhatsAppFoundationTests
{
    [Fact]
    public async Task TokenProtector_EncryptsAndMasksToken()
    {
        await using ServiceProvider provider = CreateProvider();
        var protector = provider.GetRequiredService<IWhatsAppTokenProtector>();

        string encrypted = protector.Protect("secret-token");

        Assert.NotEqual("secret-token", encrypted);
        Assert.Equal("secret-token", protector.Unprotect(encrypted));
        Assert.DoesNotContain("secret", protector.Mask(encrypted), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateConnection_RespectsWhatsappNumberLimitAndSingleDefault()
    {
        await using ServiceProvider provider = CreateProvider(numberLimit: 1);
        Guid tenantId = await SeedTenantWithPlanAsync(provider, numberLimit: 1, messageLimit: null);
        var service = provider.GetRequiredService<IWhatsAppConnectionService>();

        var first = await service.CreateConnectionAsync(CreateConnection(tenantId, "phone-1", isDefault: true));
        var second = await service.CreateConnectionAsync(CreateConnection(tenantId, "phone-2", isDefault: true));

        Assert.True(first.Succeeded, first.FirstError);
        Assert.False(second.Succeeded);
        var summary = await service.GetTenantSummaryAsync(tenantId);
        Assert.Single(summary.Connections);
        Assert.True(summary.Connections.Single().IsDefault);
    }

    [Fact]
    public async Task Connections_AreIsolatedByTenant()
    {
        await using ServiceProvider provider = CreateProvider();
        Guid tenantA = await SeedTenantWithPlanAsync(provider, "tenant-a", null, null);
        Guid tenantB = await SeedTenantWithPlanAsync(provider, "tenant-b", null, null);
        var service = provider.GetRequiredService<IWhatsAppConnectionService>();

        await service.CreateConnectionAsync(CreateConnection(tenantA, "phone-a", isDefault: true));
        await service.CreateConnectionAsync(CreateConnection(tenantB, "phone-b", isDefault: true));

        Assert.Single((await service.GetTenantSummaryAsync(tenantA)).Connections);
        Assert.Equal("phone-a", (await service.GetTenantSummaryAsync(tenantA)).Connections.Single().PhoneNumberId);
    }

    [Fact]
    public async Task WebhookVerify_RequiresExpectedToken()
    {
        await using ServiceProvider provider = CreateProvider(appSecret: "app-secret", verifyToken: "verify-me");
        var service = provider.GetRequiredService<IWhatsAppWebhookService>();

        var valid = service.Verify(new WhatsAppWebhookVerificationRequest("subscribe", "verify-me", "challenge"));
        var invalid = service.Verify(new WhatsAppWebhookVerificationRequest("subscribe", "wrong", "challenge"));

        Assert.True(valid.Succeeded);
        Assert.Equal("challenge", valid.Value);
        Assert.False(invalid.Succeeded);
    }

    [Fact]
    public async Task WebhookPost_ValidatesHmacAndStoresInboxIdempotently()
    {
        await using ServiceProvider provider = CreateProvider(appSecret: "app-secret");
        Guid tenantId = await SeedTenantWithPlanAsync(provider, null, null);
        var connectionService = provider.GetRequiredService<IWhatsAppConnectionService>();
        await connectionService.CreateConnectionAsync(CreateConnection(tenantId, "phone-1", true));
        var webhook = provider.GetRequiredService<IWhatsAppWebhookService>();
        string body = IncomingPayload("phone-1", "wamid-1");
        string signature = WhatsAppSecurity.ComputeSignature(body, "app-secret");

        var accepted = await webhook.ReceiveAsync(new WhatsAppWebhookPostRequest(body, signature));
        var duplicate = await webhook.ReceiveAsync(new WhatsAppWebhookPostRequest(body, signature));
        var missing = await webhook.ReceiveAsync(new WhatsAppWebhookPostRequest(body, null));

        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Assert.True(accepted.Succeeded, accepted.FirstError);
        Assert.True(duplicate.Value!.Duplicate);
        Assert.False(missing.Succeeded);
        Assert.Single(db.WhatsAppInboxEvents);
    }

    [Fact]
    public async Task Processor_ParsesIncomingMessagesAndStatusUpdatesWithoutDuplicates()
    {
        await using ServiceProvider provider = CreateProvider(appSecret: "app-secret");
        Guid tenantId = await SeedTenantWithPlanAsync(provider, null, null);
        var connectionService = provider.GetRequiredService<IWhatsAppConnectionService>();
        await connectionService.CreateConnectionAsync(CreateConnection(tenantId, "phone-1", true));
        var webhook = provider.GetRequiredService<IWhatsAppWebhookService>();
        var processor = provider.GetRequiredService<IWhatsAppProcessor>();
        string incoming = IncomingPayload("phone-1", "wamid-1");
        await webhook.ReceiveAsync(new WhatsAppWebhookPostRequest(incoming, WhatsAppSecurity.ComputeSignature(incoming, "app-secret")));

        var first = await processor.ProcessInboxAsync();
        var second = await processor.ProcessInboxAsync();

        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Assert.Equal(1, first.Processed);
        Assert.Equal(0, second.Processed);
        Assert.Single(db.WhatsAppMessages);
        Assert.Equal(WhatsAppMessageStatus.Received, db.WhatsAppMessages.Single().Status);
    }

    [Fact]
    public async Task QueueText_RespectsMonthlyMessageLimitAndOutboxIsProcessed()
    {
        var fake = new FakeCloudClient();
        await using ServiceProvider provider = CreateProvider(fakeClient: fake);
        Guid tenantId = await SeedTenantWithPlanAsync(provider, numberLimit: null, messageLimit: 1);
        var connectionService = provider.GetRequiredService<IWhatsAppConnectionService>();
        var create = await connectionService.CreateConnectionAsync(CreateConnection(tenantId, "phone-1", true));
        var messaging = provider.GetRequiredService<IWhatsAppMessagingService>();

        var first = await messaging.QueueTextAsync(new SendWhatsAppTextRequest(tenantId, create.Value, "5511999999999", "Olá", "idempotent-1"));
        var second = await messaging.QueueTextAsync(new SendWhatsAppTextRequest(tenantId, create.Value, "5511888888888", "Olá", "idempotent-2"));
        var processed = await provider.GetRequiredService<IWhatsAppProcessor>().ProcessOutboxAsync();

        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Assert.True(first.Succeeded, first.FirstError);
        Assert.False(second.Succeeded);
        Assert.Equal(1, processed.Processed);
        Assert.Equal(WhatsAppMessageStatus.Sent, db.WhatsAppMessages.Single().Status);
    }

    [Fact]
    public async Task Outbox_TransientFailureRetriesThenDeadLetters()
    {
        var fake = new FakeCloudClient { SendResult = new WhatsAppCloudSendResult(false, true, null, "429", "rate limited", TimeSpan.Zero) };
        await using ServiceProvider provider = CreateProvider(fakeClient: fake, retryCount: 1);
        Guid tenantId = await SeedTenantWithPlanAsync(provider, null, null);
        var connection = await provider.GetRequiredService<IWhatsAppConnectionService>().CreateConnectionAsync(CreateConnection(tenantId, "phone-1", true));
        await provider.GetRequiredService<IWhatsAppMessagingService>().QueueTextAsync(new SendWhatsAppTextRequest(tenantId, connection.Value, "5511999999999", "Olá", "retry-1"));
        var processor = provider.GetRequiredService<IWhatsAppProcessor>();

        var result = await processor.ProcessOutboxAsync();

        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Assert.Equal(1, result.DeadLetters);
        Assert.Equal(WhatsAppQueueStatus.DeadLetter, db.WhatsAppOutboxMessages.Single().Status);
        Assert.DoesNotContain("token", db.WhatsAppOutboxMessages.Single().ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateProvider(int? numberLimit = null, string appSecret = "secret", string verifyToken = "verify", IWhatsAppCloudApiClient? fakeClient = null, int retryCount = 2)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection().PersistKeysToFileSystem(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "orizon-whatsapp-test-keys")));
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddDbContext<OrizonAgentsDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IWhatsAppTokenProtector, DataProtectionWhatsAppTokenProtector>();
        services.AddSingleton(Options.Create(new WhatsAppOptions { AppSecret = appSecret, VerifyToken = verifyToken, RetryCount = retryCount, MaxPayloadBytes = 4096, ProcessorBatchSize = 10 }));
        services.AddScoped<IWhatsAppConnectionService, WhatsAppService>();
        services.AddScoped<IWhatsAppMessagingService, WhatsAppService>();
        services.AddScoped<IWhatsAppTemplateService, WhatsAppService>();
        services.AddScoped<IWhatsAppPlatformService, WhatsAppService>();
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<IWhatsAppProcessor, WhatsAppProcessor>();
        services.AddScoped(_ => fakeClient ?? new FakeCloudClient());
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedTenantWithPlanAsync(ServiceProvider provider, int? numberLimit, int? messageLimit)
        => await SeedTenantWithPlanAsync(provider, Guid.NewGuid().ToString("N")[..8], numberLimit, messageLimit);

    private static async Task<Guid> SeedTenantWithPlanAsync(ServiceProvider provider, string slug, int? numberLimit, int? messageLimit)
    {
        var db = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create($"Tenant {slug}", slug);
        SubscriptionPlan plan = SubscriptionPlan.Create($"Plano {slug}", $"PLAN_{slug}".ToUpperInvariant(), "Plano de teste", 0, 0, "BRL", 0, false, 1);
        plan.SetEntitlement(PlanFeatureKeys.WhatsAppNumbers, true, numberLimit);
        plan.SetEntitlement(PlanFeatureKeys.MonthlyMessages, true, messageLimit);
        db.Tenants.Add(tenant);
        db.SubscriptionPlans.Add(plan);
        db.TenantSubscriptions.Add(TenantSubscription.Start(tenant.Id, plan.Id, BillingCycle.Monthly, DateTime.UtcNow, 0));
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static CreateWhatsAppConnectionRequest CreateConnection(Guid tenantId, string phoneNumberId, bool isDefault)
        => new(tenantId, "Principal", "waba-1", phoneNumberId, "+55 11 99999-0000", "Orizon", "tenant-token", isDefault);

    private static string IncomingPayload(string phoneNumberId, string messageId)
        => $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "changes": [{
              "value": {
                "metadata": { "display_phone_number": "+5511999990000", "phone_number_id": "{{phoneNumberId}}" },
                "messages": [{ "from": "5511888880000", "id": "{{messageId}}", "timestamp": "1710000000", "type": "text", "text": { "body": "Oi" } }]
              },
              "field": "messages"
            }]
          }]
        }
        """;

    private sealed class FakeCloudClient : IWhatsAppCloudApiClient
    {
        public WhatsAppCloudSendResult SendResult { get; set; } = new(true, false, "wamid-out", null, null, null);

        public Task<WhatsAppCloudNumber> GetPhoneNumberAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default)
            => Task.FromResult(new WhatsAppCloudNumber(phoneNumberId, "+5511999990000", "Orizon", "Green"));

        public Task<WhatsAppCloudSendResult> SendTextAsync(string accessToken, string phoneNumberId, string recipient, string text, CancellationToken cancellationToken = default)
            => Task.FromResult(SendResult);

        public Task<WhatsAppCloudSendResult> SendTemplateAsync(string accessToken, string phoneNumberId, string recipient, string templateName, string language, CancellationToken cancellationToken = default)
            => Task.FromResult(SendResult);

        public Task<WhatsAppCloudSendResult> SendMediaAsync(string accessToken, string phoneNumberId, string recipient, string mediaId, string type, string caption, CancellationToken cancellationToken = default)
            => Task.FromResult(SendResult);

        public Task<IReadOnlyCollection<WhatsAppCloudTemplate>> GetTemplatesAsync(string accessToken, string businessAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<WhatsAppCloudTemplate>>(new[] { new WhatsAppCloudTemplate("tpl-1", "boas_vindas", "pt_BR", "UTILITY", "Approved", "[]") });
    }
}
