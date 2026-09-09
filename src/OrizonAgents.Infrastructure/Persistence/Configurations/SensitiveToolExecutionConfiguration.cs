using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Tenants;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class SensitiveToolExecutionConfiguration
    : IEntityTypeConfiguration<SensitiveToolExecution>
{
    public void Configure(EntityTypeBuilder<SensitiveToolExecution> builder)
    {
        builder.ToTable("SensitiveToolExecutions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ApprovalId).IsRequired();
        builder.Property(x => x.AgentId).IsRequired();
        builder.Property(x => x.ToolId).IsRequired();
        builder.Property(x => x.AgentToolBindingId).IsRequired();
        builder.Property(x => x.ToolKind)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.IntegrationConnectionId);
        builder.Property(x => x.ProtectedArguments)
            .HasMaxLength(SensitiveToolExecution.ProtectedArgumentsMaxLength)
            .IsRequired();
        builder.Property(x => x.InputFingerprint)
            .HasMaxLength(SensitiveToolExecution.InputFingerprintMaxLength)
            .IsRequired();
        builder.Property(x => x.ToolConfigurationFingerprint)
            .HasMaxLength(SensitiveToolExecution.ToolConfigurationFingerprintMaxLength)
            .IsRequired();
        builder.Property(x => x.State)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.ConcurrencyStamp)
            .HasMaxLength(SensitiveToolExecution.ConcurrencyStampMaxLength)
            .IsRequired()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Approval)
            .WithOne(x => x.SensitiveToolExecution)
            .HasForeignKey<SensitiveToolExecution>(x => new
            {
                x.TenantId,
                x.ApprovalId
            })
            .HasPrincipalKey<ToolExecutionApproval>(x => new
            {
                x.TenantId,
                x.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.ApprovalId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.State });
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
