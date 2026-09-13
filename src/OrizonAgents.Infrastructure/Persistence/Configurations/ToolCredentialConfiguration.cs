using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class ToolCredentialConfiguration : IEntityTypeConfiguration<ToolCredential>
{
    public void Configure(EntityTypeBuilder<ToolCredential> builder)
    {
        builder.ToTable("ToolCredentials");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(ToolCredential.NameMaxLength).IsRequired();
        builder.Property(x => x.AuthenticationType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.HeaderName).HasMaxLength(ToolCredential.HeaderNameMaxLength).IsRequired();
        builder.Property(x => x.EncryptedSecret).HasMaxLength(ToolCredential.EncryptedSecretMaxLength).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
    }
}
