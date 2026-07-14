namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string GraphApiBaseUrl { get; init; } = "https://graph.facebook.com";

    public string GraphApiVersion { get; init; } = "v20.0";

    public string? AppSecret { get; init; }

    public string? VerifyToken { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public int RetryCount { get; init; } = 2;

    public long MaxMediaBytes { get; init; } = 16 * 1024 * 1024;

    public int InboxRetentionDays { get; init; } = 30;

    public int MaxPayloadBytes { get; init; } = 256 * 1024;

    public int ProcessorBatchSize { get; init; } = 20;
}
