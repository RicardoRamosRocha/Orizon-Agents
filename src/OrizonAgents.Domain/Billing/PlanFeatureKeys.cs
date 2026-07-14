namespace OrizonAgents.Domain.Billing;

public static class PlanFeatureKeys
{
    public const string Users = "users";
    public const string Agents = "agents";
    public const string WhatsAppNumbers = "whatsapp_numbers";
    public const string KnowledgeBases = "knowledge_bases";
    public const string MonthlyMessages = "monthly_messages";
    public const string StorageMb = "storage_mb";

    public static readonly string[] All =
    [
        Users,
        Agents,
        WhatsAppNumbers,
        KnowledgeBases,
        MonthlyMessages,
        StorageMb
    ];
}
