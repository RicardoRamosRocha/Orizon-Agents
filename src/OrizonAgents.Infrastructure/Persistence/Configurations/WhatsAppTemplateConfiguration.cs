using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppTemplateConfiguration : IEntityTypeConfiguration<WhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<WhatsAppTemplate> builder)
    {
        builder.ToTable("WhatsAppTemplates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.MetaTemplateId).HasMaxLength(120).IsRequired();
        builder.Property(template => template.Name).HasMaxLength(160).IsRequired();
        builder.Property(template => template.Language).HasMaxLength(16).IsRequired();
        builder.Property(template => template.Category).HasMaxLength(64).IsRequired();
        builder.Property(template => template.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(template => template.ComponentsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(template => new { template.WhatsAppConnectionId, template.MetaTemplateId }).IsUnique();
        builder.HasIndex(template => new { template.TenantId, template.Name, template.Language });
        builder.HasOne(template => template.Connection)
            .WithMany()
            .HasForeignKey(template => template.WhatsAppConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
