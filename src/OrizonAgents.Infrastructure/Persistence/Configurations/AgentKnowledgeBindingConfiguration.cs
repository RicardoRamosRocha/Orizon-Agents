using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Knowledge;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class AgentKnowledgeBindingConfiguration :
    IEntityTypeConfiguration<AgentKnowledgeBinding>
{
    public void Configure(EntityTypeBuilder<AgentKnowledgeBinding> builder)
    {
        builder.ToTable("AgentKnowledgeBindings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.AgentId,
            x.KnowledgeBaseId
        })
        .IsUnique();

        builder.HasOne(x => x.Agent)
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.KnowledgeBase)
            .WithMany(x => x.AgentBindings)
            .HasForeignKey(x => x.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
