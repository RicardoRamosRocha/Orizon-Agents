using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppOutboxMessageConfiguration : IEntityTypeConfiguration<WhatsAppOutboxMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppOutboxMessage> builder)
    {
        builder.ToTable("WhatsAppOutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.Property(message => message.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(message => message.ErrorMessage).HasMaxLength(512);
        builder.HasIndex(message => new { message.TenantId, message.IdempotencyKey }).IsUnique();
        builder.HasIndex(message => new { message.Status, message.NextAttemptAtUtc });
        builder.HasIndex(message => message.WhatsAppMessageId);
    }
}
