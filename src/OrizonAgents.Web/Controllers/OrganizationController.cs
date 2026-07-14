using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Tenants;
using OrizonAgents.Application.Tenants.Models;
using OrizonAgents.Application.Tenants.Requests;
using OrizonAgents.Web.Models.Tenants;

namespace OrizonAgents.Web.Controllers;

[Authorize]
[Route("Admin/Organization")]
public sealed class OrganizationController : Controller
{
    private readonly ITenantManagementService _tenantManagementService;

    public OrganizationController(ITenantManagementService tenantManagementService)
    {
        _tenantManagementService = tenantManagementService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        TenantOrganizationDto? organization = await _tenantManagementService.GetOrganizationAsync(tenantId, cancellationToken);
        if (organization is null)
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        return View(ToViewModel(organization));
    }

    [HttpPost("")]
    [Authorize(Policy = "TenantAdminOnly")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(OrganizationSettingsViewModel model, CancellationToken cancellationToken)
    {
        Guid tenantId = GetTenantId();
        if (model.TenantId != tenantId)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        OperationResult result = await _tenantManagementService.UpdateOwnSettingsAsync(
            new UpdateOwnTenantSettingsRequest(
                tenantId,
                model.Name,
                model.Culture,
                model.TimeZone,
                model.ContactName,
                model.ContactEmail,
                model.ContactPhone,
                model.ConcurrencyStamp),
            cancellationToken);

        if (!result.Succeeded)
        {
            foreach (string error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        TempData["StatusMessage"] = "Organização atualizada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    private Guid GetTenantId()
    {
        string? value = User.FindFirst(OrizonClaimTypes.TenantId)?.Value;
        return Guid.TryParse(value, out Guid tenantId)
            ? tenantId
            : throw new InvalidOperationException("Usuário autenticado sem TenantId.");
    }

    private static OrganizationSettingsViewModel ToViewModel(TenantOrganizationDto organization)
    {
        return new OrganizationSettingsViewModel
        {
            TenantId = organization.Id,
            Name = organization.Name,
            Slug = organization.Slug,
            Status = organization.Status,
            Culture = organization.Culture,
            TimeZone = organization.TimeZone,
            ContactName = organization.ContactName,
            ContactEmail = organization.ContactEmail,
            ContactPhone = organization.ContactPhone,
            ConcurrencyStamp = organization.ConcurrencyStamp
        };
    }
}
