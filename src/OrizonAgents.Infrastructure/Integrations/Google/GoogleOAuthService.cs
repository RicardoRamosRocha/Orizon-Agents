using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Common.Security;
using OrizonAgents.Application.Common.Tenancy;
using OrizonAgents.Application.Common.Users;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Integrations.Google;

public sealed class GoogleOAuthService(
    OrizonAgentsDbContext db,
    ICurrentTenant tenant,
    ICurrentUser user,
    IOptions<GoogleOAuthOptions> options,
    GoogleOAuthClient google,
    GoogleOAuthStateProtector states,
    IntegrationConnectionCredentialProtector credentialsProtector,
    TimeProvider clock,
    ILogger<GoogleOAuthService> logger) : IGoogleOAuthService, IGoogleOAuthTokenService
{
    private const string NotFound = "Conexão Gmail não encontrada ou sem permissão.";
    private const string NotConfigured = "OAuth Google não configurado. Configure Integrations:Google:ClientId e Integrations:Google:ClientSecret no servidor.";
    private const string InvalidState = "A autorização expirou ou não corresponde a esta sessão. Inicie novamente a conexão com Google.";
    private const string Reauthenticate = "A autorização Google precisa ser renovada. Conecte a conta novamente.";
    private const string ConcurrentChange = "A conexão foi alterada durante a operação. Atualize a página e tente novamente.";
    private readonly GoogleOAuthOptions _options = options.Value;

    public async Task<OperationResult<string>> BeginAsync(
        Guid connectionId, string redirectUri, string correlation, CancellationToken cancellationToken = default)
    {
        if (!IsTenantAdmin())
        {
            return OperationResult<string>.Failure(NotFound);
        }
        var connection = await FindAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            return OperationResult<string>.Failure(NotFound);
        }
        if (!connection.IsActive)
        {
            return OperationResult<string>.Failure("Ative a conexão antes de conectar com Google.");
        }
        if (!_options.IsConfigured)
        {
            return OperationResult<string>.Failure(NotConfigured);
        }
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            string.IsNullOrWhiteSpace(correlation) || correlation.Length < 32 || correlation.Length > 128)
        {
            return OperationResult<string>.Failure("Não foi possível iniciar OAuth. Acesse o painel pelo endereço HTTPS correto.");
        }

        string verifier = GoogleOAuthStateProtector.NewVerifier();
        string state = states.Protect(connection.TenantId, connection.Id, user.UserId!.Value, redirectUri, correlation, verifier);
        connection.BeginOAuth(GoogleOAuthStateProtector.Hash(state));
        if (!await SaveAsync(cancellationToken))
        {
            return OperationResult<string>.Failure(ConcurrentChange);
        }
        return OperationResult<string>.Success(google.AuthorizationUrl(redirectUri, state, verifier));
    }

    public async Task<OperationResult<Guid>> CompleteAsync(
        string? state, string? code, string? error, string? correlation, CancellationToken cancellationToken = default)
    {
        var data = states.Unprotect(state);
        if (!IsTenantAdmin() || data is null || data.TenantId != tenant.TenantId ||
            data.UserId != user.UserId || string.IsNullOrWhiteSpace(correlation) || correlation.Length > 128 ||
            data.CorrelationHash != GoogleOAuthStateProtector.Hash(correlation))
        {
            return OperationResult<Guid>.Failure(InvalidState);
        }

        var connection = await FindAsync(data.ConnectionId, cancellationToken);
        if (connection is null || !connection.IsActive ||
            connection.PendingOAuthStateHash != GoogleOAuthStateProtector.Hash(state!))
        {
            return OperationResult<Guid>.Failure(InvalidState);
        }

        // Commit consumption before any outbound request. The concurrency stamp makes this single-use
        // across processes, and protects the later write against disconnect/new authorization/refresh.
        connection.ConsumeOAuthState();
        if (!await SaveAsync(cancellationToken))
        {
            return OperationResult<Guid>.Failure(InvalidState);
        }

        if (error == "access_denied")
        {
            return OperationResult<Guid>.Failure("Autorização cancelada. Você pode conectar com Google novamente.");
        }
        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code) || code.Length > 8192)
        {
            return OperationResult<Guid>.Failure("O Google não concluiu a autorização. Inicie a conexão novamente.");
        }
        if (!_options.IsConfigured)
        {
            return OperationResult<Guid>.Failure(NotConfigured);
        }

        try
        {
            GoogleTokenResponse tokens = await google.ExchangeAsync(code, data.RedirectUri, data.CodeVerifier, cancellationToken);
            GoogleAccountIdentity identity = await google.GetIdentityAsync(tokens.AccessToken, cancellationToken);
            var previous = ReadCredentials(connection);
            string? refresh = tokens.RefreshToken;
            if (string.IsNullOrWhiteSpace(refresh) && previous?.Subject == identity.Subject && previous.ClientId == _options.ClientId)
            {
                refresh = previous.RefreshToken;
            }

            var payload = new GoogleOAuthCredentials
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = string.IsNullOrWhiteSpace(refresh) ? null : refresh,
                ExpiresAtUtc = clock.GetUtcNow().AddSeconds(tokens.ExpiresInSeconds),
                Scope = tokens.Scope ?? GoogleOAuthClient.Scopes,
                Subject = identity.Subject,
                ClientId = _options.ClientId
            };
            connection.Connect(identity.Email, Protect(connection, payload));
            if (!await SaveAsync(cancellationToken))
            {
                return OperationResult<Guid>.Failure(ConcurrentChange);
            }
            return OperationResult<Guid>.Success(connection.Id);
        }
        catch (Exception exception) when (IsOAuthFailure(exception, cancellationToken))
        {
            LogFailure(connection, "callback");
            connection.MarkAuthenticationError();
            await SaveAsync(cancellationToken);
            return OperationResult<Guid>.Failure("Não foi possível confirmar a conta Google. Verifique a configuração e tente conectar novamente.");
        }
    }

    public async Task<OperationResult<GoogleAccessToken>> GetAccessTokenAsync(
        Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await FindAsync(connectionId, cancellationToken);
        if (connection is null || !connection.IsActive || connection.Status != IntegrationConnectionStatus.Connected)
        {
            return OperationResult<GoogleAccessToken>.Failure(Reauthenticate);
        }

        if (!_options.IsConfigured)
        {
            return OperationResult<GoogleAccessToken>.Failure(NotConfigured);
        }
        var payload = ReadCredentials(connection);
        if (payload is null || payload.ClientId != _options.ClientId)
        {
            return await RequireReauthenticationAsync(connection, cancellationToken);
        }
        if (payload.ExpiresAtUtc > clock.GetUtcNow().AddMinutes(1))
        {
            return OperationResult<GoogleAccessToken>.Success(new GoogleAccessToken(payload.AccessToken));
        }
        if (string.IsNullOrWhiteSpace(payload.RefreshToken))
        {
            return await RequireReauthenticationAsync(connection, cancellationToken);
        }

        try
        {
            var tokens = await google.RefreshAsync(payload.RefreshToken, cancellationToken);
            payload.AccessToken = tokens.AccessToken;
            payload.ExpiresAtUtc = clock.GetUtcNow().AddSeconds(tokens.ExpiresInSeconds);
            if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                payload.RefreshToken = tokens.RefreshToken;
            }
            payload.Scope = tokens.Scope ?? payload.Scope;
            connection.ReplaceProtectedCredentials(Protect(connection, payload));
            if (!await SaveAsync(cancellationToken))
            {
                return OperationResult<GoogleAccessToken>.Failure(ConcurrentChange);
            }
            return OperationResult<GoogleAccessToken>.Success(new GoogleAccessToken(payload.AccessToken));
        }
        catch (GoogleOAuthProtocolException exception) when (exception.RequiresReauthentication)
        {
            LogFailure(connection, "refresh_reauthentication");
            return await RequireReauthenticationAsync(connection, cancellationToken);
        }
        catch (Exception exception) when (IsOAuthFailure(exception, cancellationToken))
        {
            LogFailure(connection, "refresh_unavailable");
            return OperationResult<GoogleAccessToken>.Failure("Não foi possível renovar a autorização Google agora. Tente novamente mais tarde.");
        }
    }

    public async Task<OperationResult<bool>> DisconnectAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        if (!IsTenantAdmin())
        {
            return OperationResult<bool>.Failure(NotFound);
        }
        var connection = await FindAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            return OperationResult<bool>.Failure(NotFound);
        }

        bool remoteRevoked = connection.EncryptedCredentials is null;
        var payload = ReadCredentials(connection);
        if (payload is not null)
        {
            try
            {
                remoteRevoked = await google.RevokeAsync(payload.RefreshToken ?? payload.AccessToken, cancellationToken);
            }
            catch (Exception exception) when (IsOAuthFailure(exception, CancellationToken.None))
            {
                LogFailure(connection, "revocation_unavailable");
            }
        }

        // Local removal must finish even if the remote request times out or the browser goes away.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            connection.Disconnect();
            if (await SaveAsync(CancellationToken.None))
            {
                return OperationResult<bool>.Success(remoteRevoked);
            }
            remoteRevoked = false;
            connection = await FindAsync(connectionId, CancellationToken.None);
            if (connection is null)
            {
                return OperationResult<bool>.Success(false);
            }
        }
        return OperationResult<bool>.Failure(ConcurrentChange);
    }

    private async Task<OperationResult<GoogleAccessToken>> RequireReauthenticationAsync(
        IntegrationConnection connection, CancellationToken cancellationToken)
    {
        connection.MarkAuthenticationError();
        await SaveAsync(cancellationToken);
        return OperationResult<GoogleAccessToken>.Failure(Reauthenticate);
    }

    private string Protect(IntegrationConnection connection, GoogleOAuthCredentials payload) =>
        credentialsProtector.Protect(connection.TenantId, connection.Id, JsonSerializer.Serialize(payload));

    private GoogleOAuthCredentials? ReadCredentials(IntegrationConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.EncryptedCredentials))
        {
            return null;
        }
        try
        {
            var payload = JsonSerializer.Deserialize<GoogleOAuthCredentials>(
                credentialsProtector.Unprotect(connection.TenantId, connection.Id, connection.EncryptedCredentials));
            return payload is not null && !string.IsNullOrWhiteSpace(payload.AccessToken)
                && !string.IsNullOrWhiteSpace(payload.Subject) && !string.IsNullOrWhiteSpace(payload.ClientId)
                ? payload : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private Task<IntegrationConnection?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!tenant.HasTenant || tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
        {
            return Task.FromResult<IntegrationConnection?>(null);
        }
        return db.IntegrationConnections.SingleOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && x.Provider == IntegrationProvider.Gmail, cancellationToken);
    }

    private bool IsTenantAdmin() =>
        user.IsAuthenticated && user.UserId is Guid userId && userId != Guid.Empty &&
        tenant.HasTenant && tenant.TenantId is Guid tenantId && tenantId != Guid.Empty &&
        user.TenantId == tenantId && user.IsInRole(OrizonRoles.TenantAdmin);

    private async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }
            return false;
        }
    }

    private static bool IsOAuthFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is GoogleOAuthProtocolException or HttpRequestException or JsonException or
            CryptographicException or ArgumentException or InvalidOperationException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private void LogFailure(IntegrationConnection connection, string stage) =>
        logger.LogWarning("Google OAuth failure at {Stage}. TenantId={TenantId}, ConnectionId={ConnectionId}",
            stage, connection.TenantId, connection.Id);
}