using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Billing;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Name)
            .HasMaxLength(SubscriptionPlan.NameMaxLength)
            .IsRequired();

        builder.Property(plan => plan.Code)
            .HasMaxLength(PlanCode.MaxLength)
            .IsRequired();

        builder.Property(plan => plan.Description)
            .HasMaxLength(SubscriptionPlan.DescriptionMaxLength)
            .IsRequired();

        builder.Property(plan => plan.MonthlyPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(plan => plan.YearlyPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(plan => plan.Currency)
            .HasMaxLength(SubscriptionPlan.CurrencyMaxLength)
            .IsRequired();

        builder.Property(plan => plan.ConcurrencyStamp)
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(plan => plan.CreatedAtUtc).IsRequired();
        builder.Property(plan => plan.UpdatedAtUtc);

        builder.HasIndex(plan => plan.Code).IsUnique();
        builder.HasIndex(plan => new { plan.IsActive, plan.IsPublic, plan.SortOrder });

        builder.HasMany(plan => plan.Entitlements)
            .WithOne(entitlement => entitlement.Plan)
            .HasForeignKey(entitlement => entitlement.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
