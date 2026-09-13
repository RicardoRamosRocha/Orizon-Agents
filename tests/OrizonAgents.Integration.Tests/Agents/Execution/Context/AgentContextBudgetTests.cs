using OrizonAgents.Infrastructure.Agents.Execution.Context;

namespace OrizonAgents.Integration.Tests.Agents.Execution.Context;

public sealed class AgentContextBudgetTests
{
    [Fact]
    public void Begin_ChargesOperationalContextOnceAndAccumulatesToolResults()
    {
        var sut = new AgentContextBudget();
        var budget = sut.Begin("contexto");

        Assert.Equal(AgentContextBudget.MaximumCharactersPerExecution - 8, budget.RemainingCharacters);
        Assert.Equal("resultado-1", budget.ReduceAndConsumeToolResult("resultado-1"));
        Assert.Equal("resultado-2", budget.ReduceAndConsumeToolResult("resultado-2"));
        Assert.Equal(
            AgentContextBudget.MaximumCharactersPerExecution - 30,
            budget.RemainingCharacters);
    }

    [Fact]
    public void Begin_WhenOperationalContextConsumesBudget_ReportsExhausted()
    {
        var sut = new AgentContextBudget();
        var budget = sut.Begin(
            new string('A', AgentContextBudget.MaximumCharactersPerExecution));

        Assert.True(budget.IsExhausted);
        Assert.Empty(budget.ReduceAndConsumeToolResult("resultado"));
    }

    [Fact]
    public void ReduceToolResult_WhenContentFitsBudget_ReturnsNormalizedContent()
    {
        var sut = new AgentContextBudget();

        string result = sut.ReduceToolResult(
            "  conteúdo pequeno  ",
            1_000);

        Assert.Equal("conteúdo pequeno", result);
    }

    [Fact]
    public void ReduceToolResult_WhenRemainingBudgetIsZero_ReturnsEmpty()
    {
        var sut = new AgentContextBudget();

        string result = sut.ReduceToolResult(
            "conteúdo",
            0);

        Assert.Empty(result);
    }

    [Fact]
    public void ReduceToolResult_WhenContentIsEmpty_ReturnsEmpty()
    {
        var sut = new AgentContextBudget();

        string result = sut.ReduceToolResult(
            "   ",
            1_000);

        Assert.Empty(result);
    }

    [Fact]
    public void ReduceToolResult_WhenContentExceedsPerToolLimit_Truncates()
    {
        var sut = new AgentContextBudget();
        string content = new(
            'A',
            AgentContextBudget.MaximumCharactersPerToolResult + 1_000);

        string result = sut.ReduceToolResult(
            content,
            50_000);

        Assert.Equal(
            AgentContextBudget.MaximumCharactersPerToolResult,
            result.Length);

        Assert.Contains(
            "[Conteúdo reduzido pelo limite de contexto.]",
            result);
    }

    [Fact]
    public void ReduceToolResult_WhenRemainingBudgetIsSmallerThanPerToolLimit_RespectsRemainingBudget()
    {
        var sut = new AgentContextBudget();
        string content = new('A', 5_000);

        string result = sut.ReduceToolResult(
            content,
            1_000);

        Assert.Equal(1_000, result.Length);

        Assert.Contains(
            "[Conteúdo reduzido pelo limite de contexto.]",
            result);
    }
}
