using OrizonAgents.Domain.Common;

namespace OrizonAgents.Domain.WhatsApp;

public sealed class WhatsAppMonthlyUsage : Entity, ITenantOwnedEntity
{
    private WhatsAppMonthlyUsage()
    {
    }

    private WhatsAppMonthlyUsage(Guid tenantId, int year, int month)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (year < 2000) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        TenantId = tenantId;
        Year = year;
        Month = month;
    }

    public Guid TenantId { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public int OutgoingAcceptedCount { get; private set; }

    public static WhatsAppMonthlyUsage Create(Guid tenantId, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("Data deve estar em UTC.", nameof(utcNow));
        return new WhatsAppMonthlyUsage(tenantId, utcNow.Year, utcNow.Month);
    }

    public void IncrementOutgoingAccepted()
    {
        OutgoingAcceptedCount++;
    }
}
