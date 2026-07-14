using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppMediaConfiguration : IEntityTypeConfiguration<WhatsAppMedia>
{
    public void Configure(EntityTypeBuilder<WhatsAppMedia> builder)
    {
        builder.ToTable("WhatsAppMedia");
        builder.HasKey(media => media.Id);
        builder.Property(media => media.MetaMediaId).HasMaxLength(160).IsRequired();
        builder.Property(media => media.MimeType).HasMaxLength(120).IsRequired();
        builder.Property(media => media.FileName).HasMaxLength(260).IsRequired();
        builder.Property(media => media.Sha256).HasMaxLength(128).IsRequired();
        builder.HasIndex(media => new { media.TenantId, media.MetaMediaId }).IsUnique();
        builder.HasOne(media => media.Connection)
            .WithMany()
            .HasForeignKey(media => media.WhatsAppConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
