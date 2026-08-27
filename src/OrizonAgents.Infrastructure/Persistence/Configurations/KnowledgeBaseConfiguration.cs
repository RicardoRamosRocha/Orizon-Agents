using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Knowledge;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeBaseConfiguration :
    IEntityTypeConfiguration<KnowledgeBase>
{
    public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
    {
        builder.ToTable("KnowledgeBases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Name });

        builder.HasMany(x => x.Documents)
            .WithOne(x => x.KnowledgeBase)
            .HasForeignKey(x => x.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.AgentBindings)
            .WithOne(x => x.KnowledgeBase)
            .HasForeignKey(x => x.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
