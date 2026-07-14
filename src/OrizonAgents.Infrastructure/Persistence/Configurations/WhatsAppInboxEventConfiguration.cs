using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppInboxEventConfiguration : IEntityTypeConfiguration<WhatsAppInboxEvent>
{
    public void Configure(EntityTypeBuilder<WhatsAppInboxEvent> builder)
    {
        builder.ToTable("WhatsAppInboxEvents");
        builder.HasKey(@event => @event.Id);
        builder.Property(@event => @event.EventId).HasMaxLength(160).IsRequired();
        builder.Property(@event => @event.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(@event => @event.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(@event => @event.ErrorMessage).HasMaxLength(512);
        builder.HasIndex(@event => new { @event.TenantId, @event.EventId }).IsUnique();
        builder.HasIndex(@event => new { @event.Status, @event.NextAttemptAtUtc });
    }
}
