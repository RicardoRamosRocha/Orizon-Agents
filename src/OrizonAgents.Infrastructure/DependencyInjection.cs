using OrizonAgents.Application.Tools.Validation;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Infrastructure.Tools.Validation;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Infrastructure.Agents.Credentials;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Agents.Execution;
using OrizonAgents.Application.Agents;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Common.Email;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Common.Users;
using OrizonAgents.Infrastructure.Accounts;
using OrizonAgents.Infrastructure.Agents;
using OrizonAgents.Infrastructure.Agents.Execution;
using OrizonAgents.Infrastructure.Billing;
using OrizonAgents.Application.Dashboards;
using OrizonAgents.Infrastructure.Email;
using OrizonAgents.Infrastructure.Dashboards;
using OrizonAgents.Infrastructure.Health;
using OrizonAgents.Infrastructure.Identity;
using OrizonAgents.Application.Integrations;
using OrizonAgents.Infrastructure.Integrations;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Infrastructure.Integrations.Google;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Infrastructure.Integrations.Gmail;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Infrastructure.Tenants;
using OrizonAgents.Infrastructure.Users;
using OrizonAgents.Application.WhatsApp;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Infrastructure.WhatsApp;
using OrizonAgents.Infrastructure.Tools;
using OrizonAgents.Infrastructure.Tools.Credentials;
using OrizonAgents.Infrastructure.Tools.Execution;

using OrizonAgents.Application.Knowledge.Documents;

using OrizonAgents.Infrastructure.Knowledge.Documents.Storage;

using OrizonAgents.Infrastructure.Knowledge.Documents.Extraction;

using OrizonAgents.Infrastructure.Knowledge.Documents.Chunking;

using OrizonAgents.Infrastructure.Knowledge.Documents.Processing;

using OrizonAgents.Application.Knowledge;

using OrizonAgents.Infrastructure.Knowledge;

using OrizonAgents.Application.Knowledge.Retrieval;

using OrizonAgents.Infrastructure.Knowledge.Retrieval;

namespace OrizonAgents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool addWebSecurity = true)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");

        string redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis connection string is required.");

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantContextSetter>(provider => provider.GetRequiredService<CurrentTenant>());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IEmailSender, DevelopmentEmailSender>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAiAgentService, AiAgentService>();
        services.AddScoped<IAiAgentRunner, AiAgentRunner>();
        services.AddScoped<IAiConversationService, AiConversationService>();
        services.AddScoped<HttpAgentToolExecutor>();
        services.AddScoped<GmailAgentToolExecutor>();
        services.AddScoped<IAgentToolExecutor, AgentToolExecutor>();
        services.AddScoped<IToolExecutionApprovalService, ToolExecutionApprovalService>();
        services.AddScoped<IAgentToolInputValidator, AgentToolInputValidator>();
        services.AddScoped<IAgentModelDecisionParser, AgentModelDecisionParser>();
        services.AddScoped<IAgentToolCatalog, AgentToolCatalog>();
        services.AddScoped<IAgentToolService, AgentToolService>();
        services.AddScoped<IToolCredentialService, ToolCredentialService>();
        services.AddScoped<IToolCredentialProtector, DataProtectionToolCredentialProtector>();

        services.Configure<AgentToolHttpOptions>(
            configuration.GetSection(AgentToolHttpOptions.SectionName));

        services.AddSingleton<IAgentToolEndpointPolicy, AgentToolEndpointPolicy>();

        services.AddHttpClient("AgentTools", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IKnowledgeFileStorage, LocalKnowledgeFileStorage>();
        services.AddScoped<IKnowledgeDocumentExtractor, PlainTextDocumentExtractor>();
        services.AddScoped<IKnowledgeDocumentExtractor, PdfDocumentExtractor>();
        services.AddScoped<IKnowledgeDocumentExtractor, CsvDocumentExtractor>();
        services.AddScoped<IKnowledgeDocumentExtractor, ExcelDocumentExtractor>();
        services.AddScoped<IKnowledgeDocumentExtractor, WordDocumentExtractor>();
        services.AddScoped<IKnowledgeTextChunker, KnowledgeTextChunker>();
        services.AddScoped<IKnowledgeDocumentProcessor, KnowledgeDocumentProcessor>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<IKnowledgeRetriever, KnowledgeRetriever>();
        services.AddHttpClient<GroqChatProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.groq.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient<GeminiChatProvider>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IAiChatProvider>(provider =>
            provider.GetRequiredService<GroqChatProvider>());

        services.AddScoped<IAiChatProvider>(provider =>
            provider.GetRequiredService<GeminiChatProvider>());
        services.AddHttpClient<GeminiModelCatalog>(client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient<GroqModelCatalog>(client =>
        {
            client.BaseAddress = new Uri("https://api.groq.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IAiProviderSpecificModelCatalog>(provider =>
            provider.GetRequiredService<GeminiModelCatalog>());

        services.AddScoped<IAiProviderSpecificModelCatalog>(provider =>
            provider.GetRequiredService<GroqModelCatalog>());

        services.AddScoped<IAiProviderModelCatalog, AiProviderModelCatalog>();

        services.AddScoped<IApiCredentialService, ApiCredentialService>();
        services.AddScoped<IIntegrationConnectionService, IntegrationConnectionService>();
        services.AddScoped<IntegrationConnectionCredentialProtector>();
        services.Configure<GoogleOAuthOptions>(configuration.GetSection(GoogleOAuthOptions.SectionName));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<GoogleOAuthStateProtector>();
        services.AddScoped<GoogleOAuthClient>();
        services.AddScoped<GoogleOAuthService>();
        services.AddScoped<IGoogleOAuthService>(provider => provider.GetRequiredService<GoogleOAuthService>());
        services.AddScoped<IGoogleOAuthTokenService>(provider => provider.GetRequiredService<GoogleOAuthService>());
        services.AddScoped<IGmailClient, GmailClient>();
        services.AddHttpClient(GmailClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 1048576;
        })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            })
            .RemoveAllLoggers();
        services.AddHttpClient(GoogleOAuthClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.MaxResponseContentBufferSize = 65536;
        })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            })
            .RemoveAllLoggers();
        services.AddScoped<ITenantUserService, TenantUserService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IEntitlementService, EntitlementService>();
        services.AddScoped<IBillingCycleProcessor, BillingCycleProcessor>();
        services.AddScoped<IAiProviderCredentialService, AiProviderCredentialService>();
        services.AddScoped<IAiProviderCredentialProtector, DataProtectionAiProviderCredentialProtector>();
        services.AddScoped<IWhatsAppTokenProtector, DataProtectionWhatsAppTokenProtector>();
        services.AddScoped<IWhatsAppConnectionService, WhatsAppService>();
        services.AddScoped<IWhatsAppMessagingService, WhatsAppService>();
        services.AddScoped<IWhatsAppTemplateService, WhatsAppService>();
        services.AddScoped<IWhatsAppPlatformService, WhatsAppService>();
        services.AddScoped<IWhatsAppWebhookService, WhatsAppWebhookService>();
        services.AddScoped<IWhatsAppProcessor, WhatsAppProcessor>();
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddHttpClient<IWhatsAppCloudApiClient, WhatsAppCloudApiClient>();
        string dataProtectionKeysPath =
            configuration["DataProtection:KeysPath"]
            ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "OrizonAgents",
                "DataProtection-Keys");

        Directory.CreateDirectory(dataProtectionKeysPath);

        services
            .AddDataProtection()
            .SetApplicationName("OrizonAgents")
            .PersistKeysToFileSystem(
                new DirectoryInfo(dataProtectionKeysPath));

        services.AddDbContext<OrizonAgentsDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(OrizonAgentsDbContext).Assembly.FullName));
        });

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = configuration["Redis:InstanceName"] ?? "orizon-agents:";
        });

        services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
            })
            .AddEntityFrameworkStores<OrizonAgentsDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-OrizonAgents.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.LoginPath = "/conta/entrar";
            options.LogoutPath = "/conta/sair";
            options.AccessDeniedPath = "/conta/acesso-negado";
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        if (addWebSecurity)
        {
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("PlatformAdminOnly", policy => policy.RequireRole(OrizonRoles.PlatformAdmin));
                options.AddPolicy("TenantAdminOnly", policy => policy.RequireRole(OrizonRoles.TenantAdmin));
                options.AddPolicy("AuthenticatedAccount", policy => policy.RequireAuthenticatedUser());
            });
        }

        services.AddHealthChecks()
            .AddDbContextCheck<OrizonAgentsDbContext>(
                "postgresql",
                HealthStatus.Unhealthy)
            .AddCheck<RedisDistributedCacheHealthCheck>(
                "redis",
                HealthStatus.Unhealthy);

        return services;
    }
}
