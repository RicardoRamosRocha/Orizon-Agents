using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.Integrations;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class ApiCredentialConfiguration
    : IEntityTypeConfiguration<ApiCredential>
{
    public void Configure(EntityTypeBuilder<ApiCredential> builder)
    {
        builder.ToTable("ApiCredentials");

        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.TenantId)
            .IsRequired();

        builder.Property(credential => credential.Name)
            .HasMaxLength(ApiCredential.NameMaxLength)
            .IsRequired();

        builder.Property(credential => credential.KeyHash)
            .HasMaxLength(ApiCredential.KeyHashMaxLength)
            .IsRequired();

        builder.Property(credential => credential.IsActive)
            .IsRequired();

        builder.Property(credential => credential.CreatedAtUtc)
            .IsRequired();

        builder.Property(credential => credential.UpdatedAtUtc);

        builder.HasIndex(credential => credential.KeyHash)
            .IsUnique();

        builder.HasIndex(credential => new
        {
            credential.TenantId,
            credential.IsActive
        });
    }
}
