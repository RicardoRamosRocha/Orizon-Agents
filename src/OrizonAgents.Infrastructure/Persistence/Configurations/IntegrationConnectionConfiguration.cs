using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Domain.Tenants;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        builder.ToTable("IntegrationConnections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(IntegrationConnection.NameMaxLength).IsRequired();
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc);
        builder.Property(x => x.ConnectedAccountEmail).HasMaxLength(IntegrationConnection.ConnectedAccountEmailMaxLength);
        builder.Property(x => x.PendingOAuthStateHash).HasMaxLength(64);
        builder.Property(x => x.ConcurrencyStamp).HasMaxLength(32).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.EncryptedCredentials).HasMaxLength(IntegrationConnection.EncryptedCredentialsMaxLength);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.Provider });
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}