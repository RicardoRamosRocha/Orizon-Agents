using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Domain.Tests.Tools;

public sealed class AgentToolTests
{
    [Fact]
    public void Constructor_ShouldDefaultRiskLevelToRead()
    {
        var tool = CreateTool();

        Assert.Equal(AgentToolRiskLevel.Read, tool.RiskLevel);
    }

    [Fact]
    public void SetRiskLevel_ShouldAllowWrite()
    {
        var tool = CreateTool();

        tool.SetRiskLevel(AgentToolRiskLevel.Write);

        Assert.Equal(AgentToolRiskLevel.Write, tool.RiskLevel);
    }

    [Fact]
    public void SetRiskLevel_ShouldAllowSensitive()
    {
        var tool = CreateTool();

        tool.SetRiskLevel(AgentToolRiskLevel.Sensitive);

        Assert.Equal(AgentToolRiskLevel.Sensitive, tool.RiskLevel);
    }

    [Fact]
    public void SetRiskLevel_ShouldRejectInvalidValue()
    {
        var tool = CreateTool();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => tool.SetRiskLevel((AgentToolRiskLevel)999));
    }

    private static AgentTool CreateTool() =>
        new(
            Guid.NewGuid(),
            "Consultar disponibilidade",
            "Consulta a disponibilidade dos integrantes.",
            "https://example.com/api/availability",
            "POST");
}
