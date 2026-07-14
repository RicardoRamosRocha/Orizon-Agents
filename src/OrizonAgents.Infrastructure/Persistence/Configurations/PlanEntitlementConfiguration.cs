using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        builder.ToTable("PlanEntitlements");

        builder.HasKey(entitlement => entitlement.Id);

        builder.Property(entitlement => entitlement.FeatureKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(entitlement => entitlement.IsEnabled)
            .IsRequired();

        builder.Property(entitlement => entitlement.LimitValue);

        builder.HasIndex(entitlement => new { entitlement.SubscriptionPlanId, entitlement.FeatureKey })
            .IsUnique();
    }
}
