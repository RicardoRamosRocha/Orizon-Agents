using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(Tenant.NameMaxLength)
            .IsRequired();

        builder.Property(tenant => tenant.Slug)
            .HasMaxLength(TenantSlug.MaxLength)
            .IsRequired();

        builder.Property(tenant => tenant.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAtUtc)
            .IsRequired();

        builder.Property(tenant => tenant.UpdatedAtUtc);

        builder.HasIndex(tenant => tenant.Slug)
            .IsUnique();

        builder.HasOne(tenant => tenant.Settings)
            .WithOne(settings => settings.Tenant)
            .HasForeignKey<TenantSettings>(settings => settings.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
