using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Agents;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class AgentExecutionUsageConfiguration
    : IEntityTypeConfiguration<AgentExecutionUsage>
{
    public void Configure(EntityTypeBuilder<AgentExecutionUsage> builder)
    {
        builder.ToTable("AgentExecutionUsages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(150).IsRequired();
        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.CompletedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.StartedAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.AgentId, x.StartedAtUtc });

        builder.HasOne<AiAgent>()
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AiConversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
