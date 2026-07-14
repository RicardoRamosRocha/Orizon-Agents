using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings");

        builder.HasKey(settings => settings.Id);

        builder.Property(settings => settings.TenantId)
            .IsRequired();

        builder.Property(settings => settings.Culture)
            .HasMaxLength(TenantSettings.CultureMaxLength)
            .IsRequired();

        builder.Property(settings => settings.TimeZone)
            .HasMaxLength(TenantSettings.TimeZoneMaxLength)
            .IsRequired();

        builder.Property(settings => settings.ContactName)
            .HasMaxLength(TenantSettings.ContactNameMaxLength);

        builder.Property(settings => settings.ContactEmail)
            .HasMaxLength(TenantSettings.ContactEmailMaxLength);

        builder.Property(settings => settings.ContactPhone)
            .HasMaxLength(TenantSettings.ContactPhoneMaxLength);

        builder.Property(settings => settings.CreatedAtUtc)
            .IsRequired();

        builder.Property(settings => settings.UpdatedAtUtc);

        builder.HasIndex(settings => settings.TenantId)
            .IsUnique();
    }
}
