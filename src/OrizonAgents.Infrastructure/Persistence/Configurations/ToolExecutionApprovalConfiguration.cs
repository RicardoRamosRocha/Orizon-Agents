using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class ToolExecutionApprovalConfiguration
    : IEntityTypeConfiguration<ToolExecutionApproval>
{
    public void Configure(
        EntityTypeBuilder<ToolExecutionApproval> builder)
    {
        builder.ToTable("ToolExecutionApprovals");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.AgentId)
            .IsRequired();

        builder.Property(x => x.ToolId)
            .IsRequired();

        builder.Property(x => x.InputHash)
            .HasMaxLength(ToolExecutionApproval.InputHashMaxLength)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.AgentId,
            x.ToolId,
            x.InputHash,
            x.Status
        });

        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
