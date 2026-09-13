using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("tools/aprovacoes")]
public sealed class ToolApprovalsController : Controller
{
    private readonly IToolExecutionApprovalService _approvalService;
    private readonly ISensitiveToolExecutionRunner _executionRunner;

    public ToolApprovalsController(
        IToolExecutionApprovalService approvalService,
        ISensitiveToolExecutionRunner executionRunner)
    {
        _approvalService = approvalService;
        _executionRunner = executionRunner;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var approvals =
            await _approvalService.ListPendingAsync(cancellationToken);

        return View(approvals);
    }

    [HttpPost("{id:guid}/aprovar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        ToolExecutionApprovalResult approval =
            await _approvalService.ApproveAndGetExecutionAsync(id, cancellationToken);

        bool approved = approval.Approved && approval.ExecutionId.HasValue;

        TempData["StatusMessage"] = approved
            ? "Execução aprovada."
            : "A aprovação não está mais disponível.";

        if (approved)
        {
            SensitiveToolExecutionRunResult result =
                await _executionRunner.RunAsync(
                    approval.ExecutionId!.Value,
                    cancellationToken);

            TempData["StatusMessage"] = result.Status switch
            {
                SensitiveToolExecutionRunStatus.Executed =>
                    "Execução aprovada e concluída com sucesso.",
                SensitiveToolExecutionRunStatus.OutcomeUnknown =>
                    "A operação foi aprovada, mas o resultado externo ficou incerto.",
                _ => "A operação foi aprovada, mas não pôde ser executada devido a uma alteração de estado ou configuração."
            };
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/rejeitar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        bool rejected =
            await _approvalService.RejectAsync(id, cancellationToken);

        TempData["StatusMessage"] = rejected
            ? "Execução rejeitada."
            : "A aprovação não está mais disponível.";

        return RedirectToAction(nameof(Index));
    }
}
