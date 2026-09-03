using System.Text.Json;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class ToolExecutionInputHasherTests
{
    [Fact]
    public void Compute_ShouldIgnoreObjectPropertyOrder()
    {
        using JsonDocument first = JsonDocument.Parse(
            """
            {
              "date": "2026-09-03",
              "startsAt": "08:00:00",
              "options": {
                "active": true,
                "limit": 10
              }
            }
            """);

        using JsonDocument second = JsonDocument.Parse(
            """
            {
              "options": {
                "limit": 10,
                "active": true
              },
              "startsAt": "08:00:00",
              "date": "2026-09-03"
            }
            """);

        string firstHash =
            ToolExecutionInputHasher.Compute(first.RootElement);

        string secondHash =
            ToolExecutionInputHasher.Compute(second.RootElement);

        Assert.Equal(firstHash, secondHash);
    }

    [Fact]
    public void Compute_ShouldProduceDifferentHashWhenInputChanges()
    {
        using JsonDocument first = JsonDocument.Parse(
            """{"amount":100}""");

        using JsonDocument second = JsonDocument.Parse(
            """{"amount":1000}""");

        string firstHash =
            ToolExecutionInputHasher.Compute(first.RootElement);

        string secondHash =
            ToolExecutionInputHasher.Compute(second.RootElement);

        Assert.NotEqual(firstHash, secondHash);
    }

    [Fact]
    public void Compute_ShouldPreserveArrayOrder()
    {
        using JsonDocument first = JsonDocument.Parse(
            """{"items":[1,2,3]}""");

        using JsonDocument second = JsonDocument.Parse(
            """{"items":[3,2,1]}""");

        Assert.NotEqual(
            ToolExecutionInputHasher.Compute(first.RootElement),
            ToolExecutionInputHasher.Compute(second.RootElement));
    }

    [Fact]
    public void Compute_ShouldBeDeterministicForNullInput()
    {
        string firstHash =
            ToolExecutionInputHasher.Compute(null);

        string secondHash =
            ToolExecutionInputHasher.Compute(null);

        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
    }
}
