using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace OrizonAgents.Infrastructure.Integrations.Google;

// All destinations are fixed official Google endpoints; no browser-controlled outbound URLs.
public sealed class GoogleOAuthClient(IHttpClientFactory clients, IOptions<GoogleOAuthOptions> options)
{
    public const string HttpClientName = "GoogleOAuth";
    public const string Scopes = "openid email";
    private readonly GoogleOAuthOptions _options = options.Value;

    public string AuthorizationUrl(string redirectUri, string state, string verifier) =>
        QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "consent select_account",
            ["state"] = state,
            ["code_challenge"] = GoogleOAuthStateProtector.Challenge(verifier),
            ["code_challenge_method"] = "S256"
        });

    internal Task<GoogleTokenResponse> ExchangeAsync(string code, string redirectUri, string verifier, CancellationToken cancellationToken) =>
        RequestTokenAsync(new()
        {
            ["grant_type"] = "authorization_code", ["code"] = code,
            ["redirect_uri"] = redirectUri, ["code_verifier"] = verifier
        }, cancellationToken);

    internal Task<GoogleTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokenAsync(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken }, cancellationToken);

    private async Task<GoogleTokenResponse> RequestTokenAsync(Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        fields["client_id"] = _options.ClientId;
        fields["client_secret"] = _options.ClientSecret;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        using var client = clients.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleOAuthProtocolException(ReadString(root, "error") == "invalid_grant");
        }

        string? accessToken = ReadString(root, "access_token");
        string? tokenType = ReadString(root, "token_type");
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 8000 ||
            !string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out int seconds) ||
            seconds <= 0 || seconds > 31536000)
        {
            throw new GoogleOAuthProtocolException();
        }

        return new GoogleTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = ReadString(root, "refresh_token"),
            ExpiresInSeconds = seconds,
            Scope = ReadString(root, "scope")
        };
    }

    public async Task<GoogleAccountIdentity> GetIdentityAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var client = clients.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleOAuthProtocolException();
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;
        string? subject = ReadString(root, "sub");
        string? email = ReadString(root, "email");
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 320 ||
            !root.TryGetProperty("email_verified", out var verified) || verified.ValueKind != JsonValueKind.True)
        {
            throw new GoogleOAuthProtocolException();
        }

        return new GoogleAccountIdentity(subject, email);
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/revoke")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token })
        };
        using var client = clients.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return ReadString(json.RootElement, "error") == "invalid_token";
        }

        return false;
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

internal sealed class GoogleTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public int ExpiresInSeconds { get; init; }
    public string? Scope { get; init; }
    public override string ToString() => "[protected Google token response]";
}

public sealed record GoogleAccountIdentity(string Subject, string Email);

// Never retain provider response bodies or error_description in exceptions.
public sealed class GoogleOAuthProtocolException(bool requiresReauthentication = false) : Exception("Falha no protocolo OAuth Google.")
{
    public bool RequiresReauthentication { get; } = requiresReauthentication;
}