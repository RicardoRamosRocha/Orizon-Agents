using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Web.Models.AiCredentials;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("credenciais-ia")]
public sealed class AiCredentialsController : Controller
{
    private readonly IAiProviderCredentialService _credentialService;
    private readonly IAiProviderModelCatalog _modelCatalog;

    public AiCredentialsController(
        IAiProviderCredentialService credentialService,
        IAiProviderModelCatalog modelCatalog)
    {
        _credentialService = credentialService;
        _modelCatalog = modelCatalog;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        return View(
            await BuildViewModelAsync(cancellationToken));
    }

    [HttpGet("modelos-gemini")]
    public async Task<IActionResult> GeminiModels(
        CancellationToken cancellationToken)
    {
        var models =
            await _modelCatalog.ListAsync(AiProvider.GoogleGemini, cancellationToken);

        return Json(models);
    }

    [HttpPost("salvar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        AiProvider provider,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            TempData["ErrorMessage"] =
                "Informe a chave de API do provedor.";

            return RedirectToAction(nameof(Index));
        }

        if (!IsSupportedProvider(provider))
        {
            return BadRequest();
        }

        await _credentialService.SaveAsync(
            provider,
            apiKey,
            cancellationToken);

        TempData["StatusMessage"] =
            $"Credencial do {GetProviderName(provider)} salva com segurança.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("remover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedProvider(provider))
        {
            return BadRequest();
        }

        await _credentialService.RemoveAsync(
            provider,
            cancellationToken);

        TempData["StatusMessage"] =
            $"Credencial do {GetProviderName(provider)} removida.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<AiCredentialsIndexViewModel>
        BuildViewModelAsync(
            CancellationToken cancellationToken)
    {
        AiProvider[] providers =
        {
            AiProvider.GoogleGemini,
            AiProvider.Groq
        };

        var items =
            new List<AiProviderCredentialViewModel>();

        foreach (AiProvider provider in providers)
        {
            bool configured =
                await _credentialService.HasCredentialAsync(
                    provider,
                    cancellationToken);

            items.Add(
                new AiProviderCredentialViewModel
                {
                    Provider = provider,
                    ProviderName = GetProviderName(provider),
                    IsConfigured = configured
                });
        }

        return new AiCredentialsIndexViewModel
        {
            Providers = items
        };
    }

    private static bool IsSupportedProvider(
        AiProvider provider)
    {
        return provider is
            AiProvider.GoogleGemini or
            AiProvider.Groq;
    }

    private static string GetProviderName(
        AiProvider provider)
    {
        return provider switch
        {
            AiProvider.GoogleGemini => "Google Gemini",
            AiProvider.Groq => "Groq",
            _ => provider.ToString()
        };
    }
}