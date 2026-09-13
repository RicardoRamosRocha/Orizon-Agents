namespace OrizonAgents.Domain.Integrations;

public enum IntegrationConnectionStatus
{
    Disconnected = 0,
    PendingConfiguration = 1,
    Connected = 2,
    Error = 3
}