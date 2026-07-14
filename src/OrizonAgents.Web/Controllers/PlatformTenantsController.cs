using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Application.Tenants.Models;
using OrizonAgents.Application.Tenants.Requests;
using OrizonAgents.Web.Models.Tenants;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "PlatformAdminOnly")]
[Route("Platform/Tenants")]
public sealed class PlatformTenantsController : Controller
{
    private readonly ITenantManagementService _tenantManagementService;

    public PlatformTenantsController(ITenantManagementService tenantManagementService)
    {
        _tenantManagementService = tenantManagementService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] TenantFilterViewModel model, CancellationToken cancellationToken)
    {
        model.Result = await _tenantManagementService.ListAsync(
            new TenantListRequest(model.Search, model.Status, model.Sort, model.PageNumber),
            cancellationToken);

        return View(model);
    }

    [HttpGet("criar")]
    public IActionResult Create()
    {
        return View(new TenantFormViewModel());
    }

    [HttpPost("criar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TenantFormViewModel model, CancellationToken cancellationToken)
    {
        ValidateAdminFields(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult<Guid> result = await _tenantManagementService.CreateAsync(
            new CreateTenantRequest(
                model.Name,
                model.Slug,
                model.Culture,
                model.TimeZone,
                model.ContactName,
                model.ContactEmail,
                model.ContactPhone,
                model.AdminFullName,
                model.AdminEmail,
                model.AdminPassword,
                model.AdminConfirmPassword),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            return View(model);
        }

        TempData["StatusMessage"] = "Organização criada com sucesso.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        TenantDetailsDto? tenant = await _tenantManagementService.GetDetailsAsync(id, cancellationToken);
        return tenant is null ? NotFound() : View(tenant);
    }

    [HttpGet("{id:guid}/editar")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        TenantDetailsDto? tenant = await _tenantManagementService.GetDetailsAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        return View(new TenantFormViewModel
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Culture = tenant.Culture,
            TimeZone = tenant.TimeZone,
            ContactName = tenant.ContactName,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            ConcurrencyStamp = tenant.ConcurrencyStamp
        });
    }

    [HttpPost("{id:guid}/editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TenantFormViewModel model, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(TenantFormViewModel.AdminFullName));
        ModelState.Remove(nameof(TenantFormViewModel.AdminEmail));
        ModelState.Remove(nameof(TenantFormViewModel.AdminPassword));
        ModelState.Remove(nameof(TenantFormViewModel.AdminConfirmPassword));

        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        OperationResult result = await _tenantManagementService.UpdateAsync(
            new UpdateTenantRequest(
                id,
                model.Name,
                model.Slug,
                model.Culture,
                model.TimeZone,
                model.ContactName,
                model.ContactEmail,
                model.ContactPhone,
                model.ConcurrencyStamp),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            model.Id = id;
            return View(model);
        }

        TempData["StatusMessage"] = "Organização atualizada com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id:guid}/suspender")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        TenantDetailsDto? tenant = await _tenantManagementService.GetDetailsAsync(id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        return View(new SuspendTenantViewModel
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            ConcurrencyStamp = tenant.ConcurrencyStamp
        });
    }

    [HttpPost("{id:guid}/suspender")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, SuspendTenantViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.TenantId = id;
            return View(model);
        }

        OperationResult result = await _tenantManagementService.SuspendAsync(
            new SuspendTenantRequest(id, model.Reason, model.ConcurrencyStamp),
            cancellationToken);

        if (!result.Succeeded)
        {
            AddErrors(result.Errors);
            model.TenantId = id;
            return View(model);
        }

        TempData["StatusMessage"] = "Organização suspensa com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/reativar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, string concurrencyStamp, CancellationToken cancellationToken)
    {
        OperationResult result = await _tenantManagementService.ReactivateAsync(
            new ReactivateTenantRequest(id, concurrencyStamp),
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.FirstError;
        }
        else
        {
            TempData["StatusMessage"] = "Organização reativada com sucesso.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private void ValidateAdminFields(TenantFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.AdminFullName))
        {
            ModelState.AddModelError(nameof(model.AdminFullName), "Informe o nome do administrador.");
        }

        if (string.IsNullOrWhiteSpace(model.AdminEmail))
        {
            ModelState.AddModelError(nameof(model.AdminEmail), "Informe o e-mail do administrador.");
        }

        if (string.IsNullOrWhiteSpace(model.AdminPassword))
        {
            ModelState.AddModelError(nameof(model.AdminPassword), "Informe a senha inicial.");
        }
    }

    private void AddErrors(IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }
}
