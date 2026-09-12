namespace OrizonAgents.Infrastructure.Health;

public sealed class DatabaseGuardOptions
{
    public const string SectionName = "DatabaseGuard";

    public bool Enabled { get; set; }

    public string ExpectedHost { get; set; } = "127.0.0.1";

    public int ExpectedPort { get; set; } = 55432;

    public string ExpectedDatabase { get; set; } = "orizon_agents";

    public int ExpectedPostgresMajorVersion { get; set; } = 16;

    public bool FailOnPendingMigrations { get; set; } = true;
}
