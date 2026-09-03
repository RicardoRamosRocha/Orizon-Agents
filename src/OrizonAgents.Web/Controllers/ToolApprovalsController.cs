using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Tools.Execution;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("tools/aprovacoes")]
public sealed class ToolApprovalsController : Controller
{
    private readonly IToolExecutionApprovalService _approvalService;

    public ToolApprovalsController(
        IToolExecutionApprovalService approvalService)
    {
        _approvalService = approvalService;
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
        bool approved =
            await _approvalService.ApproveAsync(id, cancellationToken);

        TempData["StatusMessage"] = approved
            ? "Execução aprovada."
            : "A aprovação não está mais disponível.";

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
