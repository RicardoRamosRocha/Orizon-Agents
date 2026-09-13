using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Agents.Credentials;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class AiProviderCredentialConfiguration
    : IEntityTypeConfiguration<AiProviderCredential>
{
    public void Configure(
        EntityTypeBuilder<AiProviderCredential> builder)
    {
        builder.ToTable("AiProviderCredentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.EncryptedApiKey)
            .HasMaxLength(
                AiProviderCredential.EncryptedApiKeyMaxLength)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc);

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.Provider
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.TenantId,
            x.IsActive
        });
    }
}
