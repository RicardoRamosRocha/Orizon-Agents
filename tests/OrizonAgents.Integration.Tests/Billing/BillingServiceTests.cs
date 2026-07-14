using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrizonAgents.Application.Accounts;
using OrizonAgents.Application.Accounts.Requests;
using OrizonAgents.Application.Billing;
using OrizonAgents.Application.Billing.Models;
using OrizonAgents.Application.Billing.Requests;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Infrastructure.Billing;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Integration.Tests.Authentication;

namespace OrizonAgents.Integration.Tests.Billing;

public class BillingServiceTests
{
    [Fact]
    public async Task BillingSeeder_CreatesLegacyPlanAndAssignsExistingTenantsIdempotently()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        dbContext.Tenants.Add(Tenant.Create("Tenant A", "tenant-a"));
        await dbContext.SaveChangesAsync();

        await BillingSeeder.SeedAsync(provider);
        await BillingSeeder.SeedAsync(provider);

        Assert.Single(dbContext.SubscriptionPlans.Where(plan => plan.Code == PlanCode.Legacy));
        Assert.Single(dbContext.TenantSubscriptions);
    }

    [Fact]
    public async Task CreatePlanAndAssignPlan_CreateSubscriptionAndHistory()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IBillingService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        OperationResult<Guid> plan = await service.CreatePlanAsync(CreatePlan(limitUsers: 2));

        OperationResult<Guid> subscription = await service.AssignPlanAsync(new AssignSubscriptionRequest(tenant.Id, plan.Value, BillingCycle.Monthly, true, null));

        Assert.True(subscription.Succeeded, subscription.FirstError);
        Assert.Single(await service.GetHistoryAsync(subscription.Value));
    }

    [Fact]
    public async Task EntitlementService_ReturnsLimitedAndUnlimitedUsage()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var service = provider.GetRequiredService<IBillingService>();
        var entitlements = provider.GetRequiredService<IEntitlementService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        OperationResult<Guid> plan = await service.CreatePlanAsync(CreatePlan(limitUsers: 1));
        await service.AssignPlanAsync(new AssignSubscriptionRequest(tenant.Id, plan.Value, BillingCycle.Monthly, false, null));

        EntitlementUsageDto usage = await entitlements.GetUsageAsync(tenant.Id, PlanFeatureKeys.Users);

        Assert.True(usage.IsEnabled);
        Assert.Equal(1, usage.LimitValue);
        Assert.Equal(1, usage.Available);
    }

    [Fact]
    public async Task TenantUserService_BlocksCreateWhenUserLimitReached()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var billing = provider.GetRequiredService<IBillingService>();
        var users = provider.GetRequiredService<ITenantUserService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        OperationResult<Guid> plan = await billing.CreatePlanAsync(CreatePlan(limitUsers: 1));
        await billing.AssignPlanAsync(new AssignSubscriptionRequest(tenant.Id, plan.Value, BillingCycle.Monthly, false, null));

        OperationResult<Guid> first = await users.CreateAsync(CreateUser(tenant.Id, "Ana", "ana@orizon.test"));
        OperationResult<Guid> second = await users.CreateAsync(CreateUser(tenant.Id, "Bia", "bia@orizon.test"));

        Assert.True(first.Succeeded, first.FirstError);
        Assert.False(second.Succeeded);
        Assert.Contains("limite de usuários", second.FirstError);
    }

    [Fact]
    public async Task BillingCycleProcessor_IsIdempotentForExpiredTrial()
    {
        await using ServiceProvider provider = AuthenticationTestFixture.CreateServiceProvider();
        var billing = provider.GetRequiredService<IBillingService>();
        var dbContext = provider.GetRequiredService<OrizonAgentsDbContext>();
        Tenant tenant = Tenant.Create("Tenant A", "tenant-a");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        OperationResult<Guid> plan = await billing.CreatePlanAsync(CreatePlan(trialDays: 1, limitUsers: null));
        OperationResult<Guid> subscriptionId = await billing.AssignPlanAsync(new AssignSubscriptionRequest(tenant.Id, plan.Value, BillingCycle.Monthly, true, null));
        TenantSubscription subscription = await dbContext.TenantSubscriptions.SingleAsync(subscription => subscription.Id == subscriptionId.Value);
        dbContext.Entry(subscription).Property(nameof(TenantSubscription.TrialEndsAtUtc)).CurrentValue = DateTime.UtcNow.AddDays(-1);
        await dbContext.SaveChangesAsync();
        var processor = new BillingCycleProcessor(dbContext, NullLogger<BillingCycleProcessor>.Instance);

        int first = await processor.ProcessAsync(DateTime.UtcNow);
        int second = await processor.ProcessAsync(DateTime.UtcNow);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    private static PlanUpsertRequest CreatePlan(int? limitUsers, int trialDays = 0)
    {
        return new PlanUpsertRequest(
            "Plano Teste",
            Guid.NewGuid().ToString("N")[..8],
            "Plano para testes",
            10,
            100,
            "BRL",
            trialDays,
            true,
            1,
            null,
            PlanFeatureKeys.All.Select(key => new PlanEntitlementRequest(key, true, key == PlanFeatureKeys.Users ? limitUsers : null)).ToArray());
    }

    private static CreateTenantUserRequest CreateUser(Guid tenantId, string fullName, string email)
    {
        return new CreateTenantUserRequest(tenantId, fullName, email, AuthenticationTestFixture.ValidPassword, AuthenticationTestFixture.ValidPassword, OrizonAgents.Application.Common.Security.OrizonRoles.TenantMember);
    }
}
