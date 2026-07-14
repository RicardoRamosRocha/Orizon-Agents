namespace OrizonAgents.Domain.WhatsApp;

public enum WhatsAppQueueStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3,
    DeadLetter = 4
}
