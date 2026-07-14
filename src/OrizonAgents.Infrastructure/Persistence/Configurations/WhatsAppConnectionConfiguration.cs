using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppConnectionConfiguration : IEntityTypeConfiguration<WhatsAppConnection>
{
    public void Configure(EntityTypeBuilder<WhatsAppConnection> builder)
    {
        builder.ToTable("WhatsAppConnections");
        builder.HasKey(connection => connection.Id);
        builder.Property(connection => connection.Name).HasMaxLength(WhatsAppConnection.NameMaxLength).IsRequired();
        builder.Property(connection => connection.WhatsAppBusinessAccountId).HasMaxLength(WhatsAppConnection.BusinessAccountIdMaxLength).IsRequired();
        builder.Property(connection => connection.PhoneNumberId).HasMaxLength(WhatsAppConnection.PhoneNumberIdMaxLength).IsRequired();
        builder.Property(connection => connection.DisplayPhoneNumber).HasMaxLength(WhatsAppConnection.DisplayPhoneNumberMaxLength).IsRequired();
        builder.Property(connection => connection.VerifiedName).HasMaxLength(WhatsAppConnection.VerifiedNameMaxLength).IsRequired();
        builder.Property(connection => connection.EncryptedAccessToken).HasMaxLength(WhatsAppConnection.EncryptedTokenMaxLength).IsRequired();
        builder.Property(connection => connection.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(connection => connection.QualityRating).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(connection => connection.ConcurrencyStamp).HasMaxLength(WhatsAppConnection.ConcurrencyStampMaxLength).IsConcurrencyToken();
        builder.HasIndex(connection => connection.PhoneNumberId).IsUnique();
        builder.HasIndex(connection => new { connection.TenantId, connection.IsDefault })
            .IsUnique()
            .HasFilter("\"IsDefault\" = true");
        builder.HasIndex(connection => new { connection.TenantId, connection.Status });
        builder.HasOne<OrizonAgents.Domain.Tenants.Tenant>()
            .WithMany()
            .HasForeignKey(connection => connection.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
