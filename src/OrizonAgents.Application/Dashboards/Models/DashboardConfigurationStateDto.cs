namespace OrizonAgents.Application.Dashboards.Models;

public sealed record DashboardConfigurationStateDto(
    string Label,
    string Status,
    string Detail,
    string Tone);
