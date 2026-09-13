using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Knowledge;
using Pgvector;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeChunkEmbeddingConfiguration :
    IEntityTypeConfiguration<KnowledgeChunkEmbedding>
{
    private const int EmbeddingDimensions = 1536;

    public void Configure(EntityTypeBuilder<KnowledgeChunkEmbedding> builder)
    {
        builder.ToTable("KnowledgeChunkEmbeddings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Model)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.Dimensions)
            .IsRequired();

        var embeddingProperty = builder.Property(x => x.Embedding)
            .HasConversion(
                values => new Vector(values),
                vector => vector.ToArray())
            .HasColumnType($"vector({EmbeddingDimensions})")
            .IsRequired();

        embeddingProperty.Metadata.SetValueComparer(
            new ValueComparer<float[]>(
                (left, right) =>
                    ReferenceEquals(left, right) ||
                    (left != null && right != null && left.SequenceEqual(right)),
                value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                value => value.ToArray()));

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.KnowledgeChunkId,
            x.Provider,
            x.Model
        })
        .IsUnique();

        builder.HasOne(x => x.KnowledgeChunk)
            .WithMany()
            .HasForeignKey(x => x.KnowledgeChunkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
