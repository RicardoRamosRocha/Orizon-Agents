namespace OrizonAgents.Application.Dashboards.Models;

public sealed record SetupChecklistItemDto(
    string Label,
    string Description,
    bool IsComplete);
