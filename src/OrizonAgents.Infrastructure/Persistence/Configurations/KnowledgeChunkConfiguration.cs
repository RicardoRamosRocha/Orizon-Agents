using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Knowledge;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeChunkConfiguration :
    IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("KnowledgeChunks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Position)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.DocumentId,
            x.Position
        })
        .IsUnique();
    }
}
