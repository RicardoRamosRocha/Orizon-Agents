namespace OrizonAgents.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; protected set; }

    public DateTime? UpdatedAtUtc { get; protected set; }

    public void MarkCreated(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        CreatedAtUtc = utcNow;
    }

    public void MarkUpdated(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        UpdatedAtUtc = utcNow;
    }

    private static void EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Dates must be provided in UTC.", nameof(dateTime));
        }
    }
}
