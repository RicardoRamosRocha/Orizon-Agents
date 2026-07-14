using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Domain.Billing;
using OrizonAgents.Domain.Common;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Domain.WhatsApp;
using OrizonAgents.Infrastructure.Identity;

namespace OrizonAgents.Infrastructure.Persistence;

public sealed class OrizonAgentsDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ICurrentTenant _currentTenant;

    public OrizonAgentsDbContext(
        DbContextOptions<OrizonAgentsDbContext> options,
        ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();

    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();

    public DbSet<WhatsAppConnection> WhatsAppConnections => Set<WhatsAppConnection>();

    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();

    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();

    public DbSet<WhatsAppMedia> WhatsAppMedia => Set<WhatsAppMedia>();

    public DbSet<WhatsAppInboxEvent> WhatsAppInboxEvents => Set<WhatsAppInboxEvent>();

    public DbSet<WhatsAppOutboxMessage> WhatsAppOutboxMessages => Set<WhatsAppOutboxMessage>();

    public DbSet<WhatsAppMonthlyUsage> WhatsAppMonthlyUsage => Set<WhatsAppMonthlyUsage>();

    public override int SaveChanges()
    {
        ApplyAuditDates();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditDates();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrizonAgentsDbContext).Assembly);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private void ApplyAuditDates()
    {
        DateTime utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.MarkCreated(utcNow);
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.MarkUpdated(utcNow);
            }
        }
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwnedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(OrizonAgentsDbContext)
                .GetMethod(nameof(CreateTenantQueryFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            var filter = (LambdaExpression)method.Invoke(this, Array.Empty<object>())!;
            entityType.SetQueryFilter(filter);
        }
    }

    private Expression<Func<TEntity, bool>> CreateTenantQueryFilter<TEntity>()
        where TEntity : class, ITenantOwnedEntity
    {
        return entity => !_currentTenant.HasTenant || entity.TenantId == _currentTenant.TenantId;
    }
}
