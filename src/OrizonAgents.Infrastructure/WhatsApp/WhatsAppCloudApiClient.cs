using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrizonAgents.Infrastructure.WhatsApp;

public sealed class WhatsAppCloudApiClient : IWhatsAppCloudApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppCloudApiClient> _logger;

    public WhatsAppCloudApiClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppCloudApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.GraphApiBaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
    }

    public async Task<WhatsAppCloudNumber> GetPhoneNumberAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"{Version()}/{phoneNumberId}?fields=display_phone_number,verified_name,quality_rating", accessToken);
        using HttpResponseMessage response = await SendWithRetryAsync(request, idempotent: true, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(SanitizeError(json));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new WhatsAppCloudNumber(
            phoneNumberId,
            GetString(root, "display_phone_number") ?? string.Empty,
            GetString(root, "verified_name") ?? string.Empty,
            GetString(root, "quality_rating") ?? "UNKNOWN");
    }

    public Task<WhatsAppCloudSendResult> SendTextAsync(string accessToken, string phoneNumberId, string recipient, string text, CancellationToken cancellationToken = default)
        => SendMessageAsync(accessToken, phoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = recipient,
            type = "text",
            text = new { body = text, preview_url = false }
        }, cancellationToken);

    public Task<WhatsAppCloudSendResult> SendTemplateAsync(string accessToken, string phoneNumberId, string recipient, string templateName, string language, CancellationToken cancellationToken = default)
        => SendMessageAsync(accessToken, phoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = recipient,
            type = "template",
            template = new { name = templateName, language = new { code = language } }
        }, cancellationToken);

    public Task<WhatsAppCloudSendResult> SendMediaAsync(string accessToken, string phoneNumberId, string recipient, string mediaId, string type, string caption, CancellationToken cancellationToken = default)
        => SendMessageAsync(accessToken, phoneNumberId, new
        {
            messaging_product = "whatsapp",
            to = recipient,
            type,
            image = type.Equals("image", StringComparison.OrdinalIgnoreCase) ? new { id = mediaId, caption } : null,
            document = type.Equals("document", StringComparison.OrdinalIgnoreCase) ? new { id = mediaId, caption } : null,
            audio = type.Equals("audio", StringComparison.OrdinalIgnoreCase) ? new { id = mediaId } : null,
            video = type.Equals("video", StringComparison.OrdinalIgnoreCase) ? new { id = mediaId, caption } : null
        }, cancellationToken);

    public async Task<IReadOnlyCollection<WhatsAppCloudTemplate>> GetTemplatesAsync(string accessToken, string businessAccountId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, $"{Version()}/{businessAccountId}/message_templates?fields=id,name,language,category,status,components", accessToken);
        using HttpResponseMessage response = await SendWithRetryAsync(request, idempotent: true, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(SanitizeError(json));
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array) return Array.Empty<WhatsAppCloudTemplate>();
        return data.EnumerateArray()
            .Select(item => new WhatsAppCloudTemplate(
                GetString(item, "id") ?? $"{GetString(item, "name")}-{GetString(item, "language")}",
                GetString(item, "name") ?? string.Empty,
                GetString(item, "language") ?? string.Empty,
                GetString(item, "category") ?? string.Empty,
                GetString(item, "status") ?? "UNKNOWN",
                item.TryGetProperty("components", out JsonElement components) ? components.GetRawText() : "[]"))
            .ToArray();
    }

    private async Task<WhatsAppCloudSendResult> SendMessageAsync(string accessToken, string phoneNumberId, object payload, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, $"{Version()}/{phoneNumberId}/messages", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await SendWithRetryAsync(request, idempotent: false, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            bool transient = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
            return new WhatsAppCloudSendResult(false, transient, null, ((int)response.StatusCode).ToString(), SanitizeError(json), response.Headers.RetryAfter?.Delta);
        }

        string? id = null;
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("messages", out JsonElement messages) && messages.ValueKind == JsonValueKind.Array)
        {
            id = messages.EnumerateArray().Select(item => GetString(item, "id")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        return new WhatsAppCloudSendResult(true, false, id ?? Guid.NewGuid().ToString("N"), null, null, null);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, bool idempotent, CancellationToken cancellationToken)
    {
        int attempts = idempotent ? Math.Max(1, _options.RetryCount + 1) : 1;
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage response = await _httpClient.SendAsync(CloneRequest(request), cancellationToken);
            if (attempt >= attempts || response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
            {
                return response;
            }

            TimeSpan delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(200 * attempt);
            _logger.LogWarning("WhatsApp Cloud API transient failure {StatusCode}; retrying idempotent request.", (int)response.StatusCode);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content is not null)
        {
            string body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            clone.Content = new StringContent(body, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        return clone;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string accessToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private string Version() => _options.GraphApiVersion.Trim().Trim('/');

    private static string? GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string SanitizeError(string value)
    {
        string cleaned = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        return cleaned.Length <= 512 ? cleaned : cleaned[..512];
    }
}
