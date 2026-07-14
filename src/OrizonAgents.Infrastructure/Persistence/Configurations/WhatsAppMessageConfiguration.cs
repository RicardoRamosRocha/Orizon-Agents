using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppMessageConfiguration : IEntityTypeConfiguration<WhatsAppMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessage> builder)
    {
        builder.ToTable("WhatsAppMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.ExternalMessageId).HasMaxLength(WhatsAppMessage.ExternalMessageIdMaxLength);
        builder.Property(message => message.Direction).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(message => message.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(message => message.Sender).HasMaxLength(WhatsAppMessage.PhoneMaxLength).IsRequired();
        builder.Property(message => message.Recipient).HasMaxLength(WhatsAppMessage.PhoneMaxLength).IsRequired();
        builder.Property(message => message.TextContent).HasMaxLength(WhatsAppMessage.TextMaxLength);
        builder.Property(message => message.MediaId).HasMaxLength(WhatsAppMessage.ExternalMessageIdMaxLength);
        builder.Property(message => message.TemplateName).HasMaxLength(160);
        builder.Property(message => message.ErrorCode).HasMaxLength(WhatsAppMessage.ErrorCodeMaxLength);
        builder.Property(message => message.ErrorMessage).HasMaxLength(WhatsAppMessage.ErrorMessageMaxLength);
        builder.HasIndex(message => new { message.TenantId, message.ExternalMessageId }).IsUnique().HasFilter("\"ExternalMessageId\" IS NOT NULL");
        builder.HasIndex(message => new { message.TenantId, message.CreatedAtUtc });
        builder.HasIndex(message => new { message.TenantId, message.Status });
        builder.HasOne(message => message.Connection)
            .WithMany()
            .HasForeignKey(message => message.WhatsAppConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
