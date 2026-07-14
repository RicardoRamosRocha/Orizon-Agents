using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionHistoryConfiguration : IEntityTypeConfiguration<SubscriptionHistory>
{
    public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        builder.ToTable("SubscriptionHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Event)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(history => history.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(history => history.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(history => history.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(history => history.OccurredAtUtc)
            .IsRequired();

        builder.HasIndex(history => new { history.TenantSubscriptionId, history.Event, history.OccurredAtUtc });
        builder.HasIndex(history => history.TenantId);

        builder.HasOne(history => history.Tenant)
            .WithMany()
            .HasForeignKey(history => history.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(history => history.Subscription)
            .WithMany()
            .HasForeignKey(history => history.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
