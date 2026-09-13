using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class AgentToolBindingConfiguration
    : IEntityTypeConfiguration<AgentToolBinding>
{
    public void Configure(EntityTypeBuilder<AgentToolBinding> builder)
    {
        builder.ToTable("AgentToolBindings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.AgentId)
            .IsRequired();

        builder.Property(x => x.ToolId)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.AgentId,
            x.ToolId
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.AgentId,
            x.IsActive
        });

        builder.HasOne<AiAgent>()
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AgentTool>()
            .WithMany()
            .HasForeignKey(x => x.ToolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
