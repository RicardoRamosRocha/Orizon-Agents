using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class AgentToolConfiguration : IEntityTypeConfiguration<AgentTool>
{
    public void Configure(EntityTypeBuilder<AgentTool> builder)
    {
        builder.ToTable("AgentTools");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Endpoint).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Kind)
          .HasConversion<string>()
          .HasMaxLength(30)
          .IsRequired();

        builder.Property(x => x.IntegrationConnectionId);
        builder.Property(x => x.RiskLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.InputSchema).HasColumnType("jsonb");
        builder.Property(x => x.IsActive).IsRequired();
        builder.HasOne(x => x.ToolCredential)
            .WithMany(x => x.Tools)
            .HasForeignKey(x => x.ToolCredentialId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrizonAgents.Domain.Integrations.IntegrationConnection>()
            .WithMany()
            .HasForeignKey(x => x.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.HasIndex(x => new { x.TenantId, x.IntegrationConnectionId });
    }
}
