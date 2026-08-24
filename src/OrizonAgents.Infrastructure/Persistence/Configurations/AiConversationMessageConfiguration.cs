using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class AiConversationMessageConfiguration
    : IEntityTypeConfiguration<AiConversationMessage>
{
    public void Configure(
        EntityTypeBuilder<AiConversationMessage> builder)
    {
        builder.ToTable("AiConversationMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.ConversationId)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.ConversationId,
            x.CreatedAtUtc
        });
    }
}
