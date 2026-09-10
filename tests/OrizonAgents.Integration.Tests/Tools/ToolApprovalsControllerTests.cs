using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Web.Controllers;

namespace OrizonAgents.Integration.Tests.Tools;

public sealed class ToolApprovalsControllerTests
{
    [Theory]
    [InlineData(SensitiveToolExecutionRunStatus.Executed, "concluída com sucesso")]
    [InlineData(SensitiveToolExecutionRunStatus.OutcomeUnknown, "resultado externo ficou incerto")]
    [InlineData(SensitiveToolExecutionRunStatus.NotAvailable, "não pôde ser executada")]
    public async Task Approve_ApprovedExecution_RunsOnceAndShowsSafeFeedback(
        SensitiveToolExecutionRunStatus status,
        string expectedMessage)
    {
        var approvals = new StubApprovalService(
            ToolExecutionApprovalResult.Success(Guid.NewGuid()));
        var runner = new StubRunner(new SensitiveToolExecutionRunResult(status));
        ToolApprovalsController controller = CreateController(approvals, runner);

        IActionResult action = await controller.Approve(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(1, approvals.ApproveCount);
        Assert.Equal(1, runner.RunCount);
        Assert.Contains(expectedMessage, controller.TempData["StatusMessage"]?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_UnavailableOrSecondAttempt_DoesNotRunAgain()
    {
        var approvals = new StubApprovalService(
            ToolExecutionApprovalResult.Success(Guid.NewGuid()),
            ToolExecutionApprovalResult.NotAvailable());
        var runner = new StubRunner(SensitiveToolExecutionRunResult.Executed());
        ToolApprovalsController controller = CreateController(approvals, runner);

        await controller.Approve(Guid.NewGuid(), CancellationToken.None);
        await controller.Approve(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(2, approvals.ApproveCount);
        Assert.Equal(1, runner.RunCount);
        Assert.Contains("não está mais disponível", controller.TempData["StatusMessage"]?.ToString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reject_NeverRunsSensitiveExecution()
    {
        var approvals = new StubApprovalService(ToolExecutionApprovalResult.NotAvailable());
        var runner = new StubRunner(SensitiveToolExecutionRunResult.Executed());
        ToolApprovalsController controller = CreateController(approvals, runner);

        await controller.Reject(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(0, runner.RunCount);
    }

    private static ToolApprovalsController CreateController(
        IToolExecutionApprovalService approvals,
        ISensitiveToolExecutionRunner runner)
    {
        var controller = new ToolApprovalsController(approvals, runner)
        {
            TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                new InMemoryTempDataProvider())
        };

        return controller;
    }

    private sealed class StubApprovalService(params ToolExecutionApprovalResult[] results)
        : IToolExecutionApprovalService
    {
        private int _resultIndex;
        public int ApproveCount { get; private set; }

        public Task<ToolExecutionApprovalResult> ApproveAndGetExecutionAsync(Guid approvalId, CancellationToken cancellationToken = default)
        {
            ApproveCount++;
            return Task.FromResult(results[Math.Min(_resultIndex++, results.Length - 1)]);
        }

        public Task<bool> ApproveAsync(Guid approvalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<ToolExecutionAuthorizationResult> AuthorizeAsync(Guid agentId, AgentTool tool, AgentToolBinding binding, JsonElement? input, CancellationToken cancellationToken = default) =>
            Task.FromResult(ToolExecutionAuthorizationResult.Allowed());

        public Task<IReadOnlyList<ToolExecutionApprovalListItemDto>> ListPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ToolExecutionApprovalListItemDto>>(Array.Empty<ToolExecutionApprovalListItemDto>());

        public Task<bool> RejectAsync(Guid approvalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class StubRunner(SensitiveToolExecutionRunResult result) : ISensitiveToolExecutionRunner
    {
        public int RunCount { get; private set; }
        public Task<SensitiveToolExecutionRunResult> RunAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            RunCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
