using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Knowledge;
using OrizonAgents.Application.Knowledge.Models;
using OrizonAgents.Application.Knowledge.Requests;
using OrizonAgents.Web.Models.Knowledge;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("conhecimento")]
public sealed class KnowledgeController : Controller
{
    private readonly IKnowledgeService _knowledgeService;

    public KnowledgeController(IKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<KnowledgeBaseListItemDto> bases =
            await _knowledgeService.ListAsync(cancellationToken);

        return View(bases);
    }

    [HttpGet("nova")]
    public IActionResult Create()
    {
        return View(new CreateKnowledgeBaseViewModel());
    }

    [HttpPost("nova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateKnowledgeBaseViewModel form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        OperationResult<Guid> result =
            await _knowledgeService.CreateAsync(
                new CreateKnowledgeBaseRequest(
                    form.Name,
                    form.Description),
                cancellationToken);

        if (!result.Succeeded || result.Value == Guid.Empty)
        {
            AddErrors(result.Errors);
            return View(form);
        }

        TempData["StatusMessage"] =
            "Base de conhecimento criada com sucesso.";

        return RedirectToAction(
            nameof(Details),
            new { id = result.Value });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken)
    {
        KnowledgeBaseDetailsDto? knowledgeBase =
            await _knowledgeService.GetAsync(
                id,
                cancellationToken);

        if (knowledgeBase is null)
        {
            return NotFound();
        }

        return View(knowledgeBase);
    }

    [HttpPost("{id:guid}/documentos")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024 + 64 * 1024)]
    public async Task<IActionResult> Upload(
        Guid id,
        UploadKnowledgeDocumentViewModel form,
        CancellationToken cancellationToken)
    {
        if (form.KnowledgeBaseId != id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid || form.File is null)
        {
            TempData["StatusMessage"] =
                "Selecione um documento válido.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        await using Stream stream =
            form.File.OpenReadStream();

        OperationResult<Guid> result =
            await _knowledgeService.UploadDocumentAsync(
                new UploadKnowledgeDocumentRequest(
                    id,
                    form.File.FileName,
                    form.File.ContentType,
                    form.File.Length,
                    stream),
                cancellationToken);

        TempData["StatusMessage"] = result.Succeeded
            ? "Documento enviado. Agora ele pode ser processado."
            : string.Join(" ", result.Errors);

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    [HttpPost("{id:guid}/documentos/{documentId:guid}/processar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        OperationResult result =
            await _knowledgeService.ProcessDocumentAsync(
                documentId,
                cancellationToken);

        TempData["StatusMessage"] = result.Succeeded
            ? "Documento processado com sucesso."
            : string.Join(" ", result.Errors);

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    [HttpPost("{id:guid}/documentos/{documentId:guid}/excluir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        OperationResult result =
            await _knowledgeService.DeleteDocumentAsync(
                documentId,
                cancellationToken);

        TempData["StatusMessage"] = result.Succeeded
            ? "Documento excluído."
            : string.Join(" ", result.Errors);

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(
                string.Empty,
                error);
        }
    }
}
