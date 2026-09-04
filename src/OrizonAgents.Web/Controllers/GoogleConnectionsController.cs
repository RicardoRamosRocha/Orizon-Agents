using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OrizonAgents.Application.Integrations.Google;

namespace OrizonAgents.Web.Controllers;

[Authorize(Policy = "TenantAdminOnly")]
[Route("integracoes/conexoes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GoogleConnectionsController(IGoogleOAuthService oauth) : Controller
{
    [HttpPost("{id:guid}/google/conectar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        string? redirectUri = Url.Action(nameof(Callback), "GoogleConnections", values: null, protocol: Request.Scheme);
        if (redirectUri is null)
        {
            TempData["StatusMessage"] = "Não foi possível determinar o endereço de retorno do Google.";
            return RedirectToAction("Details", "Connections", new { id });
        }

        string correlation = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var result = await oauth.BeginAsync(id, redirectUri, correlation, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["StatusMessage"] = string.Join(" ", result.Errors);
            return RedirectToAction("Details", "Connections", new { id });
        }

        string authorizationUrl = result.Value!;
        string state = QueryHelpers.ParseQuery(new Uri(authorizationUrl).Query)["state"].ToString();
        Response.Cookies.Append(CookieName(state), correlation, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(10)
        });
        return Redirect(authorizationUrl);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> Callback(
        string? state, string? code, string? error, CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        string? correlation = null;
        if (!string.IsNullOrWhiteSpace(state) && state.Length <= 8192)
        {
            string cookieName = CookieName(state);
            Request.Cookies.TryGetValue(cookieName, out correlation);
            Response.Cookies.Delete(cookieName, new CookieOptions
            {
                Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/"
            });
        }

        var result = await oauth.CompleteAsync(state, code, error, correlation, cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? "Conta Google conectada com sucesso."
            : string.Join(" ", result.Errors);
        // Never render the callback query or include it in subsequent navigation.
        return result.Succeeded
            ? RedirectToAction("Details", "Connections", new { id = result.Value })
            : RedirectToAction("Index", "Connections");
    }

    [HttpPost("{id:guid}/google/desconectar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken cancellationToken)
    {
        var result = await oauth.DisconnectAsync(id, cancellationToken);
        TempData["StatusMessage"] = !result.Succeeded
            ? string.Join(" ", result.Errors)
            : result.Value
                ? "Google desconectado. As credenciais locais foram removidas."
                : "Credenciais locais removidas. Não foi possível confirmar a revogação no Google; remova também o acesso do aplicativo na sua Conta Google.";
        return RedirectToAction("Details", "Connections", new { id });
    }

    private static string CookieName(string state) =>
        "__Host-Orizon.GoogleOAuth." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)))[..32];
}