using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Users;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Infrastructure.Integrations;
using OrizonAgents.Infrastructure.Integrations.Google;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class GoogleOAuthTests
{
    [Fact]
    public async Task Begin_UsesMinimalScopesOfflineAccessAndPkce_AndRejectsOtherTenant()
    {
        await using var f = new Fixture();
        var query = await f.Begin();
        Assert.Equal("openid email", query["scope"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("consent select_account", query["prompt"]);
        Assert.Equal(Fixture.RedirectUri, query["redirect_uri"]);
        Assert.False(query.ContainsKey("client_secret"));
        Assert.False(query.ContainsKey("code_verifier"));
        Assert.NotEqual(query["state"], f.Connection.PendingOAuthStateHash);
        var other = new IntegrationConnection(Guid.NewGuid(), "Outra", IntegrationProvider.Gmail);
        f.Db.Add(other);
        await f.Db.SaveChangesAsync();
        var denied = await f.Service.BeginAsync(other.Id, Fixture.RedirectUri, Fixture.Correlation);
        Assert.False(denied.Succeeded);
        Assert.Null(other.PendingOAuthStateHash);
        Assert.Empty(f.Http.Requests);
    }

    [Theory]
    [InlineData("clientId")]
    [InlineData("clientSecret")]
    public async Task MissingConfiguration_IsControlled(string missing)
    {
        await using var f = new Fixture();
        if (missing == "clientId") f.Options.ClientId = "";
        else f.Options.ClientSecret = "";
        var result = await f.Service.BeginAsync(f.Connection.Id, Fixture.RedirectUri, Fixture.Correlation);
        Assert.False(result.Succeeded);
        Assert.Contains("não configurado", result.FirstError);
        Assert.Null(f.Connection.PendingOAuthStateHash);
        Assert.Empty(f.Http.Requests);
    }

    [Fact]
    public async Task SuccessfulCallback_VerifiesIdentityAndProtectsBothTokens_AndStateCannotReplay()
    {
        await using var f = new Fixture();
        var query = await f.Begin();
        f.EnqueueAuthorization();
        var result = await f.Service.CompleteAsync(query["state"], "test-code", null, Fixture.Correlation);
        Assert.True(result.Succeeded);
        Assert.Equal(IntegrationConnectionStatus.Connected, f.Connection.Status);
        Assert.Equal("account@example.com", f.Connection.ConnectedAccountEmail);
        Assert.Null(f.Connection.PendingOAuthStateHash);
        Assert.DoesNotContain(Fixture.AccessToken, f.Connection.EncryptedCredentials!);
        Assert.DoesNotContain(Fixture.RefreshToken, f.Connection.EncryptedCredentials!);
        var stored = f.ReadPayload();
        Assert.Equal(Fixture.AccessToken, stored.GetProperty("AccessToken").GetString());
        Assert.Equal(Fixture.RefreshToken, stored.GetProperty("RefreshToken").GetString());
        Assert.Equal("subject-one", stored.GetProperty("Subject").GetString());
        Assert.True(stored.GetProperty("ExpiresAtUtc").GetDateTimeOffset() > f.Clock.GetUtcNow());
        var exchange = QueryHelpers.ParseQuery(f.Http.Requests[0].Body!);
        Assert.Equal(Fixture.RedirectUri, exchange["redirect_uri"]);
        Assert.Equal(Fixture.ClientSecret, exchange["client_secret"]);
        Assert.Equal(query["code_challenge"], GoogleOAuthStateProtector.Challenge(exchange["code_verifier"].ToString()));
        Assert.Equal("Bearer " + Fixture.AccessToken, f.Http.Requests[1].Authorization);
        Assert.DoesNotContain(Fixture.AccessToken, f.Http.Requests[1].Uri);
        var dto = await new IntegrationConnectionService(f.Db, f.Tenant).GetAsync(f.Connection.Id);
        string serialized = JsonSerializer.Serialize(dto);
        Assert.Contains("account@example.com", serialized);
        Assert.DoesNotContain(Fixture.AccessToken, serialized);
        Assert.DoesNotContain(Fixture.RefreshToken, serialized);
        Assert.False((await f.Service.CompleteAsync(query["state"], "test-code", null, Fixture.Correlation)).Succeeded);
        Assert.Equal(2, f.Http.Requests.Count);
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("expired")]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("cookie")]
    [InlineData("anonymous")]
    [InlineData("role")]
    [InlineData("missing")]
    public async Task Callback_RejectsInvalidStateOrPrincipalWithoutCallingGoogle(string reason)
    {
        await using var f = new Fixture();
        string? state = (await f.Begin())["state"];
        string correlation = Fixture.Correlation;
        switch (reason)
        {
            case "tampered": state = "invalid" + state; break;
            case "expired": f.Clock.Advance(TimeSpan.FromMinutes(11)); break;
            case "tenant":
                f.Tenant.SetTenantId(Guid.NewGuid());
                f.User.TenantId = f.Tenant.TenantId;
                break;
            case "user": f.User.UserId = Guid.NewGuid(); break;
            case "cookie": correlation = "wrong-browser"; break;
            case "anonymous": f.User.IsAuthenticated = false; break;
            case "role": f.User.Admin = false; break;
            case "missing": state = null; break;
        }
        Assert.False((await f.Service.CompleteAsync(state, "test-code", null, correlation)).Succeeded);
        Assert.Equal(IntegrationConnectionStatus.PendingConfiguration, f.Connection.Status);
        Assert.Null(f.Connection.EncryptedCredentials);
        Assert.Empty(f.Http.Requests);
    }

    [Theory]
    [InlineData("access_denied", "test-code")]
    [InlineData(null, null)]
    [InlineData("server_error", null)]
    public async Task CancelledOrIncompleteCallback_ConsumesStateWithoutMarkingConnectedOrError(string? error, string? code)
    {
        await using var f = new Fixture();
        string state = (await f.Begin())["state"];
        var result = await f.Service.CompleteAsync(state, code, error, Fixture.Correlation);
        Assert.False(result.Succeeded);
        Assert.Null(f.Connection.PendingOAuthStateHash);
        Assert.Equal(IntegrationConnectionStatus.PendingConfiguration, f.Connection.Status);
        Assert.Empty(f.Http.Requests);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("userinfo")]
    [InlineData("unverified")]
    [InlineData("malformed")]
    [InlineData("timeout")]
    public async Task ProviderFailure_IsControlledAndDoesNotLeakSecrets(string failure)
    {
        await using var f = new Fixture();
        string state = (await f.Begin())["state"];
        if (failure == "token")
        {
            f.Http.Enqueue(Json(new { error = "invalid_grant", error_description = Fixture.AccessToken }, HttpStatusCode.BadRequest));
        }
        else if (failure == "malformed")
        {
            f.Http.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json " + Fixture.AccessToken) });
        }
        else if (failure == "timeout")
        {
            f.Http.Enqueue(_ => throw new TaskCanceledException(Fixture.AccessToken));
        }
        else
        {
            f.EnqueueToken();
            f.Http.Enqueue(failure == "userinfo"
                ? Json(new { error = Fixture.AccessToken }, HttpStatusCode.ServiceUnavailable)
                : Json(new { sub = "subject-one", email = "account@example.com", email_verified = false }));
        }
        var result = await f.Service.CompleteAsync(state, "test-code", null, Fixture.Correlation);
        Assert.False(result.Succeeded);
        Assert.Equal(IntegrationConnectionStatus.Error, f.Connection.Status);
        Assert.Null(f.Connection.EncryptedCredentials);
        Assert.DoesNotContain(Fixture.AccessToken, string.Join(" ", result.Errors));
        Assert.DoesNotContain(Fixture.AccessToken, string.Join(" ", f.Log.Messages));
    }

    [Fact]
    public async Task ValidAccessToken_IsReturnedWithoutHttp_AndExecutionResultRedactsSerialization()
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        var result = await f.Service.GetAccessTokenAsync(f.Connection.Id);
        Assert.True(result.Succeeded);
        Assert.Equal(Fixture.AccessToken, result.Value!.Value);
        Assert.DoesNotContain(Fixture.AccessToken, result.Value.ToString());
        Assert.DoesNotContain(Fixture.AccessToken, JsonSerializer.Serialize(result));
        Assert.Empty(f.Http.Requests);
    }

    [Theory]
    [InlineData("openid email", false)]
    [InlineData("openid email https://www.googleapis.com/auth/gmail.readonly", true)]
    [InlineData("https://www.googleapis.com/auth/gmail.readonly email openid", true)]
    [InlineData("  openid\t email\r\n https://www.googleapis.com/auth/gmail.readonly  ", true)]
    [InlineData("openid email https://www.googleapis.com/auth/gmail.readonly.extra", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void GrantedScopes_AreAnExactWhitespaceSeparatedSet_AndFailClosedForGmail(
        string? grantedScopes,
        bool expected)
    {
        Assert.Equal(expected, GoogleOAuthScopeCatalog.HasCapability(grantedScopes, GoogleOAuthCapability.GmailRead));
    }

    [Fact]
    public async Task ExistingIdentityConnection_RemainsUsableButHasNoGmailCapability()
    {
        await using var f = new Fixture();
        await f.SeedConnected(scope: "openid email");

        var token = await f.Service.GetAccessTokenAsync(f.Connection.Id);

        Assert.True(token.Succeeded);
        Assert.True(await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.BasicIdentity));
        Assert.False(await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.GmailRead));
        string serialized = JsonSerializer.Serialize(new
        {
            HasGmail = await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.GmailRead)
        });
        Assert.DoesNotContain(Fixture.AccessToken, serialized);
        Assert.DoesNotContain(Fixture.RefreshToken, serialized);
        Assert.DoesNotContain("openid", serialized);
        Assert.Empty(f.Http.Requests);
    }

    [Fact]
    public async Task CallbackWithoutProviderScope_DoesNotAssumeRequestedScopes_AndConnectionStillWorks()
    {
        await using var f = new Fixture();
        string state = (await f.Begin())["state"];
        f.EnqueueToken(scope: null);
        f.EnqueueIdentity();

        Assert.True((await f.Service.CompleteAsync(state, "test-code", null, Fixture.Correlation)).Succeeded);
        Assert.Equal(string.Empty, f.ReadPayload().GetProperty("Scope").GetString());
        Assert.True((await f.Service.GetAccessTokenAsync(f.Connection.Id)).Succeeded);
        Assert.False(await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.GmailRead));
    }

    [Fact]
    public async Task ExpiredAccessToken_IsRefreshedAndPreservesPreviousRefreshToken()
    {
        await using var f = new Fixture();
        await f.SeedConnected(expired: true);
        string oldCiphertext = f.Connection.EncryptedCredentials!;
        f.EnqueueToken("renewed-access", refresh: null);
        var result = await f.Service.GetAccessTokenAsync(f.Connection.Id);
        Assert.True(result.Succeeded);
        Assert.Equal("renewed-access", result.Value!.Value);
        var payload = f.ReadPayload();
        Assert.Equal(Fixture.RefreshToken, payload.GetProperty("RefreshToken").GetString());
        Assert.NotEqual(oldCiphertext, f.Connection.EncryptedCredentials);
        Assert.DoesNotContain("renewed-access", f.Connection.EncryptedCredentials!);
        Assert.True(payload.GetProperty("ExpiresAtUtc").GetDateTimeOffset() > f.Clock.GetUtcNow());
        Assert.Equal("refresh_token", QueryHelpers.ParseQuery(Assert.Single(f.Http.Requests).Body!)["grant_type"]);
        Assert.Equal(Fixture.RefreshToken, QueryHelpers.ParseQuery(f.Http.Requests[0].Body!)["refresh_token"]);
    }

    [Fact]
    public async Task RefreshWithoutProviderScope_PreservesKnownScopesAndDoesNotInventGmail()
    {
        await using var f = new Fixture();
        await f.SeedConnected(expired: true, scope: "openid email");
        f.EnqueueToken("renewed-access", refresh: null, scope: null);

        Assert.True((await f.Service.GetAccessTokenAsync(f.Connection.Id)).Succeeded);
        Assert.Equal("openid email", f.ReadPayload().GetProperty("Scope").GetString());
        Assert.True(await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.BasicIdentity));
        Assert.False(await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.GmailRead));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("revoked")]
    [InlineData("corrupted")]
    public async Task UnusableRefresh_RequiresReauthentication(string reason)
    {
        await using var f = new Fixture();
        await f.SeedConnected(expired: true, refresh: reason == "missing" ? null : Fixture.RefreshToken);
        if (reason == "revoked")
            f.Http.Enqueue(Json(new { error = "invalid_grant", error_description = Fixture.RefreshToken }, HttpStatusCode.BadRequest));
        if (reason == "corrupted")
        {
            f.Connection.ReplaceProtectedCredentials("unreadable-protected-payload");
            await f.Db.SaveChangesAsync();
        }
        var result = await f.Service.GetAccessTokenAsync(f.Connection.Id);
        Assert.False(result.Succeeded);
        Assert.Equal(IntegrationConnectionStatus.Error, f.Connection.Status);
        Assert.Contains("novamente", result.FirstError);
        Assert.DoesNotContain(Fixture.RefreshToken, string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task TransientRefreshFailure_PreservesCredentialsForRetry()
    {
        await using var f = new Fixture();
        await f.SeedConnected(expired: true);
        string previous = f.Connection.EncryptedCredentials!;
        f.Http.Enqueue(Json(new { error = "temporarily_unavailable" }, HttpStatusCode.ServiceUnavailable));
        Assert.False((await f.Service.GetAccessTokenAsync(f.Connection.Id)).Succeeded);
        Assert.Equal(IntegrationConnectionStatus.Connected, f.Connection.Status);
        Assert.Equal(previous, f.Connection.EncryptedCredentials);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReauthorizationWithoutRefresh_PreservesOnlySameAccountToken(bool sameAccount)
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        string state = (await f.Begin())["state"];
        f.EnqueueToken(refresh: null);
        f.EnqueueIdentity(sameAccount ? "subject-one" : "subject-two");
        Assert.True((await f.Service.CompleteAsync(state, "test-code", null, Fixture.Correlation)).Succeeded);
        var refresh = f.ReadPayload().GetProperty("RefreshToken");
        if (sameAccount) Assert.Equal(Fixture.RefreshToken, refresh.GetString());
        else Assert.Equal(JsonValueKind.Null, refresh.ValueKind);
    }

    [Fact]
    public async Task ReauthorizationWithDifferentClientId_DoesNotReuseOldRefresh()
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        f.Options.ClientId = "new-client-id";
        string state = (await f.Begin())["state"];
        f.EnqueueToken(refresh: null);
        f.EnqueueIdentity();
        Assert.True((await f.Service.CompleteAsync(state, "test-code", null, Fixture.Correlation)).Succeeded);
        Assert.Equal(JsonValueKind.Null, f.ReadPayload().GetProperty("RefreshToken").ValueKind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Disconnect_ClearsCredentialsEvenIfRemoteFails_AndAllowsReconnect(bool remoteSuccess)
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        f.Http.Enqueue(Json(new { }, remoteSuccess ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable));
        var result = await f.Service.DisconnectAsync(f.Connection.Id);
        Assert.True(result.Succeeded);
        Assert.Equal(remoteSuccess, result.Value);
        Assert.Equal(IntegrationConnectionStatus.Disconnected, f.Connection.Status);
        Assert.Null(f.Connection.EncryptedCredentials);
        Assert.Null(f.Connection.ConnectedAccountEmail);
        Assert.Null(f.Connection.PendingOAuthStateHash);
        var request = Assert.Single(f.Http.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://oauth2.googleapis.com/revoke", request.Uri);
        Assert.Equal(Fixture.RefreshToken, QueryHelpers.ParseQuery(request.Body!)["token"]);
        string state = (await f.Begin())["state"];
        f.EnqueueAuthorization();
        Assert.True((await f.Service.CompleteAsync(state, "new-code", null, Fixture.Correlation)).Succeeded);
        Assert.Equal(IntegrationConnectionStatus.Connected, f.Connection.Status);
    }

    [Fact]
    public async Task OtherTenant_CannotDisconnectOrResolveTokens()
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        string previous = f.Connection.EncryptedCredentials!;
        f.Tenant.SetTenantId(Guid.NewGuid());
        f.User.TenantId = f.Tenant.TenantId;
        Assert.False((await f.Service.DisconnectAsync(f.Connection.Id)).Succeeded);
        Assert.False((await f.Service.GetAccessTokenAsync(f.Connection.Id)).Succeeded);
        Assert.False(await f.Service.HasCapabilityAsync(f.Connection.Id, GoogleOAuthCapability.GmailRead));
        Assert.Equal(previous, f.Connection.EncryptedCredentials);
        Assert.Empty(f.Http.Requests);
    }

    [Fact]
    public async Task NewAttempt_AndDisconnect_InvalidateOldCallback()
    {
        await using var f = new Fixture();
        string oldState = (await f.Begin())["state"];
        string newState = (await f.Begin())["state"];
        Assert.False((await f.Service.CompleteAsync(oldState, "code", null, Fixture.Correlation)).Succeeded);
        Assert.True((await f.Service.DisconnectAsync(f.Connection.Id)).Succeeded);
        Assert.False((await f.Service.CompleteAsync(newState, "code", null, Fixture.Correlation)).Succeeded);
        Assert.Empty(f.Http.Requests);
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("refresh")]
    public async Task ConcurrentDisconnect_CannotBeUndoneByLateProviderResponse(string operation)
    {
        await using var f = new Fixture();
        string? state = null;
        if (operation == "refresh") await f.SeedConnected(expired: true);
        else state = (await f.Begin())["state"];
        f.Http.Enqueue(async _ =>
        {
            await using var otherDb = new OrizonAgentsDbContext(f.DbOptions, f.Tenant);
            var connection = await otherDb.IntegrationConnections.SingleAsync();
            connection.Disconnect();
            await otherDb.SaveChangesAsync();
            return Fixture.TokenResponse();
        });
        if (operation == "callback")
        {
            f.EnqueueIdentity();
            Assert.False((await f.Service.CompleteAsync(state, "code", null, Fixture.Correlation)).Succeeded);
        }
        else Assert.False((await f.Service.GetAccessTokenAsync(f.Connection.Id)).Succeeded);
        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.IntegrationConnections.SingleAsync();
        Assert.Null(stored.EncryptedCredentials);
        Assert.Equal(IntegrationConnectionStatus.Disconnected, stored.Status);
    }

    [Fact]
    public async Task CredentialsMustBeDisconnectedBeforeDeletingRegistration()
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        var admin = new IntegrationConnectionService(f.Db, f.Tenant);
        Assert.False((await admin.DeleteAsync(f.Connection.Id)).Succeeded);
        f.Http.Enqueue(Json(new { }));
        Assert.True((await f.Service.DisconnectAsync(f.Connection.Id)).Succeeded);
        Assert.True((await admin.DeleteAsync(f.Connection.Id)).Succeeded);
    }


    [Theory]
    [InlineData("inactive")]
    [InlineData("noTenant")]
    [InlineData("noUser")]
    [InlineData("role")]
    [InlineData("http")]
    public async Task Begin_RejectsUnavailableConnectionOrUntrustedContext(string reason)
    {
        await using var f = new Fixture();
        string redirectUri = Fixture.RedirectUri;
        switch (reason)
        {
            case "inactive": f.Connection.Deactivate(); await f.Db.SaveChangesAsync(); break;
            case "noTenant": f.Tenant.Clear(); break;
            case "noUser": f.User.IsAuthenticated = false; break;
            case "role": f.User.Admin = false; break;
            case "http": redirectUri = "http://orizon.example/integracoes/conexoes/google/callback"; break;
        }
        Assert.False((await f.Service.BeginAsync(f.Connection.Id, redirectUri, Fixture.Correlation)).Succeeded);
        Assert.Null(f.Connection.PendingOAuthStateHash);
        Assert.Empty(f.Http.Requests);
    }

    [Fact]
    public async Task RefreshWithoutServerConfiguration_DoesNotInvalidateStoredAuthorization()
    {
        await using var f = new Fixture();
        await f.SeedConnected(expired: true);
        string encrypted = f.Connection.EncryptedCredentials!;
        f.Options.ClientId = "";
        var result = await f.Service.GetAccessTokenAsync(f.Connection.Id);
        Assert.False(result.Succeeded);
        Assert.Contains("não configurado", result.FirstError);
        Assert.Equal(IntegrationConnectionStatus.Connected, f.Connection.Status);
        Assert.Equal(encrypted, f.Connection.EncryptedCredentials);
        Assert.Empty(f.Http.Requests);
    }

    [Fact]
    public async Task DisconnectTimeout_StillRemovesCredentials()
    {
        await using var f = new Fixture();
        await f.SeedConnected();
        f.Http.Enqueue(_ => throw new TaskCanceledException(Fixture.RefreshToken));
        var result = await f.Service.DisconnectAsync(f.Connection.Id);
        Assert.True(result.Succeeded);
        Assert.False(result.Value);
        Assert.Null(f.Connection.EncryptedCredentials);
        Assert.Equal(IntegrationConnectionStatus.Disconnected, f.Connection.Status);
        Assert.DoesNotContain(Fixture.RefreshToken, string.Join(" ", f.Log.Messages));
    }

    [Fact]
    public async Task AdminEdit_HandlesConcurrentOAuthWithoutOverwritingCredentials()
    {
        await using var f = new Fixture();
        await using var otherDb = new OrizonAgentsDbContext(f.DbOptions, f.Tenant);
        await otherDb.IntegrationConnections.SingleAsync();
        await f.SeedConnected();
        var staleAdmin = new IntegrationConnectionService(otherDb, f.Tenant);
        var result = await staleAdmin.UpdateAsync(f.Connection.Id, new("Stale edit"));
        Assert.False(result.Succeeded);
        Assert.Equal("E-mail Comercial", (await f.Db.IntegrationConnections.AsNoTracking().SingleAsync()).Name);
        Assert.NotNull((await f.Db.IntegrationConnections.AsNoTracking().SingleAsync()).EncryptedCredentials);
    }

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class Fixture : IAsyncDisposable
    {
        public const string RedirectUri = "https://orizon.example/integracoes/conexoes/google/callback";
        public const string Correlation = "test-browser-correlation-32-characters-minimum";
        public const string ClientSecret = "test-only-client-secret";
        public const string AccessToken = "test-only-access-token";
        public const string RefreshToken = "test-only-refresh-token";
        public readonly CurrentTenant Tenant = new();
        public readonly TestUser User = new();
        public readonly TestClock Clock = new();
        public readonly FakeHttp Http = new();
        public readonly TestLogger Log = new();
        public readonly GoogleOAuthOptions Options = new() { ClientId = "test-only-client-id", ClientSecret = ClientSecret };
        public readonly IntegrationConnectionCredentialProtector Protector;
        public readonly OrizonAgentsDbContext Db;
        public readonly DbContextOptions<OrizonAgentsDbContext> DbOptions;
        public readonly IntegrationConnection Connection;
        public readonly GoogleOAuthService Service;

        public Fixture()
        {
            Tenant.SetTenantId(Guid.NewGuid());
            User.TenantId = Tenant.TenantId;
            DbOptions = new DbContextOptionsBuilder<OrizonAgentsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Db = new OrizonAgentsDbContext(DbOptions, Tenant);
            Connection = new IntegrationConnection(Tenant.TenantId!.Value, "E-mail Comercial", IntegrationProvider.Gmail);
            Db.Add(Connection);
            Db.SaveChanges();
            var protection = new EphemeralDataProtectionProvider();
            Protector = new IntegrationConnectionCredentialProtector(protection);
            var options = Microsoft.Extensions.Options.Options.Create(Options);
            Service = new GoogleOAuthService(Db, Tenant, User, options,
                new GoogleOAuthClient(new FakeFactory(Http), options),
                new GoogleOAuthStateProtector(protection, Clock), Protector, Clock, Log);
        }

        public async Task<Dictionary<string, string>> Begin()
        {
            var result = await Service.BeginAsync(Connection.Id, RedirectUri, Correlation);
            Assert.True(result.Succeeded, result.FirstError);
            return QueryHelpers.ParseQuery(new Uri(result.Value!).Query).ToDictionary(x => x.Key, x => x.Value.ToString());
        }

        public void EnqueueAuthorization()
        {
            EnqueueToken();
            EnqueueIdentity();
        }
        public void EnqueueToken(
            string access = AccessToken,
            string? refresh = RefreshToken,
            string? scope = GoogleOAuthScopeCatalog.BasicIdentityRequest) =>
            Http.Enqueue(TokenResponse(access, refresh, scope));
        public static HttpResponseMessage TokenResponse(
            string access = AccessToken,
            string? refresh = RefreshToken,
            string? scope = GoogleOAuthScopeCatalog.BasicIdentityRequest) =>
            Json(new { access_token = access, refresh_token = refresh, expires_in = 3600, token_type = "Bearer", scope });
        public void EnqueueIdentity(string subject = "subject-one") =>
            Http.Enqueue(Json(new { sub = subject, email = "account@example.com", email_verified = true }));

        public async Task SeedConnected(
            bool expired = false,
            string? refresh = RefreshToken,
            string? scope = GoogleOAuthScopeCatalog.BasicIdentityRequest)
        {
            string json = JsonSerializer.Serialize(new
            {
                AccessToken,
                RefreshToken = refresh,
                ExpiresAtUtc = Clock.GetUtcNow().AddMinutes(expired ? -5 : 60),
                TokenType = "Bearer",
                Scope = scope,
                Subject = "subject-one",
                ClientId = Options.ClientId
            });
            Connection.Connect("account@example.com", Protector.Protect(Connection.TenantId, Connection.Id, json));
            await Db.SaveChangesAsync();
        }

        public JsonElement ReadPayload() => JsonSerializer.Deserialize<JsonElement>(
            Protector.Unprotect(Connection.TenantId, Connection.Id, Connection.EncryptedCredentials!));
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            Http.Dispose();
        }
    }

    private sealed class TestUser : ICurrentUser
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public Guid? TenantId { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool Admin { get; set; } = true;
        public bool IsInRole(string role) => Admin && role == OrizonRoles.TenantAdmin;
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    private sealed record RecordedRequest(string Method, string Uri, string? Body, string? Authorization);
    private sealed class FakeHttp : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _responses = new();
        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(_ => Task.FromResult(response));
        public void Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) => _responses.Enqueue(response);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.Method.Method, request.RequestUri!.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString()));
            Assert.NotEmpty(_responses);
            return await _responses.Dequeue()(request);
        }
    }

    private sealed class FakeFactory(FakeHttp handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(GoogleOAuthClient.HttpClientName, name);
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class TestLogger : ILogger<GoogleOAuthService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception);
            Messages.Add(formatter(state, exception));
        }
    }
}
