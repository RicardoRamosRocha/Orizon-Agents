using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Infrastructure.Identity;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.TenantId);

        builder.Property(user => user.FullName)
            .HasMaxLength(ApplicationUser.FullNameMaxLength)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.Property(user => user.UpdatedAtUtc);

        builder.Property(user => user.LastLoginAtUtc);

        builder.HasIndex(user => user.TenantId);

        builder.HasOne(user => user.Tenant)
            .WithMany()
            .HasForeignKey(user => user.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
