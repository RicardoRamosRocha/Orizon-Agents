namespace OrizonAgents.Application.Dashboards.Models;

public sealed record DashboardMetricDto(
    string Label,
    int Value,
    string Description,
    string Tone);
