using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");

        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(subscription => subscription.BillingCycle)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(subscription => subscription.ConcurrencyStamp)
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(subscription => subscription.StartedAtUtc).IsRequired();
        builder.Property(subscription => subscription.TrialEndsAtUtc);
        builder.Property(subscription => subscription.CurrentPeriodStartUtc).IsRequired();
        builder.Property(subscription => subscription.CurrentPeriodEndUtc).IsRequired();
        builder.Property(subscription => subscription.CanceledAtUtc);
        builder.Property(subscription => subscription.CreatedAtUtc).IsRequired();
        builder.Property(subscription => subscription.UpdatedAtUtc);

        builder.HasIndex(subscription => subscription.TenantId)
            .IsUnique();

        builder.HasIndex(subscription => new { subscription.Status, subscription.CurrentPeriodEndUtc });

        builder.HasOne(subscription => subscription.Tenant)
            .WithMany()
            .HasForeignKey(subscription => subscription.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(subscription => subscription.Plan)
            .WithMany()
            .HasForeignKey(subscription => subscription.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
