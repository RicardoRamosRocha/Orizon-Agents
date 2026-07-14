using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrizonAgents.Domain.WhatsApp;

namespace OrizonAgents.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppMonthlyUsageConfiguration : IEntityTypeConfiguration<WhatsAppMonthlyUsage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMonthlyUsage> builder)
    {
        builder.ToTable("WhatsAppMonthlyUsage");
        builder.HasKey(usage => usage.Id);
        builder.HasIndex(usage => new { usage.TenantId, usage.Year, usage.Month }).IsUnique();
    }
}
