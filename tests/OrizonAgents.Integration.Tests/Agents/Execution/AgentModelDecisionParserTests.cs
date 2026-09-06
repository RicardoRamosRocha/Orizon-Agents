using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Infrastructure.Tools.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class AgentModelDecisionParserTests
{
    private readonly AgentModelDecisionParser _parser = new();

    [Fact]
    public void Parse_LegacyToolCall_ReturnsSingleCall()
    {
        Guid toolId = Guid.NewGuid();
        string response =
            $"{{\"action\":\"tool_call\",\"toolId\":\"{toolId}\"," +
            "\"input\":{\"messageId\":\"message-1\"}}";

        AgentModelDecision decision = _parser.Parse(response);

        Assert.Equal(AgentModelDecisionType.ToolCall, decision.Type);
        AgentToolCall call = Assert.Single(decision.ToolCalls);
        Assert.Equal(toolId, call.ToolId);
        Assert.Same(call, decision.ToolCall);
        Assert.Equal(
            "message-1",
            call.Input!.Value.GetProperty("messageId").GetString());
    }

    [Fact]
    public void Parse_ToolCalls_ReturnsCallsInOriginalOrder()
    {
        Guid firstToolId = Guid.NewGuid();
        Guid secondToolId = Guid.NewGuid();
        string response =
            $"{{\"action\":\"tool_calls\",\"calls\":[" +
            $"{{\"toolId\":\"{firstToolId}\",\"input\":{{\"order\":1}}}}," +
            $"{{\"toolId\":\"{secondToolId}\",\"input\":{{\"order\":2}}}}" +
            "]}";

        AgentModelDecision decision = _parser.Parse(response);

        Assert.Equal(AgentModelDecisionType.ToolCall, decision.Type);
        Assert.Equal(2, decision.ToolCalls.Count);
        Assert.Null(decision.ToolCall);
        Assert.Equal(firstToolId, decision.ToolCalls[0].ToolId);
        Assert.Equal(
            1,
            decision.ToolCalls[0].Input!.Value
                .GetProperty("order")
                .GetInt32());
        Assert.Equal(secondToolId, decision.ToolCalls[1].ToolId);
        Assert.Equal(
            2,
            decision.ToolCalls[1].Input!.Value
                .GetProperty("order")
                .GetInt32());
    }

    [Theory]
    [InlineData("""{"action":"tool_calls"}""")]
    [InlineData("""{"action":"tool_calls","calls":[]}""")]
    [InlineData("""{"action":"tool_calls","calls":{}}""")]
    [InlineData("""{"action":"tool_calls","calls":[{"toolId":"invalid"}]}""")]
    [InlineData(
        """{"action":"tool_calls","calls":[{"toolId":"11111111-1111-1111-1111-111111111111"},{}]}""")]
    [InlineData("""{"action":123,"calls":[]}""")]
    [InlineData("""{"action":"tool_calls","calls":["invalid"]}""")]
    [InlineData("[]")]
    [InlineData("{not-json")]
    public void Parse_InvalidBatch_FallsBackToSafeFinalResponse(
        string response)
    {
        AgentModelDecision decision = _parser.Parse(response);

        Assert.Equal(AgentModelDecisionType.Response, decision.Type);
        Assert.Equal(response, decision.Response);
        Assert.Empty(decision.ToolCalls);
    }
}
