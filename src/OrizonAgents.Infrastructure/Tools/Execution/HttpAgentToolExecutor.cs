using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Tools;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class HttpAgentToolExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAgentToolEndpointPolicy _endpointPolicy;
    private readonly IToolCredentialService _credentialService;
    private readonly AgentToolHttpOptions _httpOptions;
    private readonly ILogger<HttpAgentToolExecutor> _logger;

    public HttpAgentToolExecutor(
        IHttpClientFactory httpClientFactory,
        IAgentToolEndpointPolicy endpointPolicy,
        IToolCredentialService credentialService,
        IOptions<AgentToolHttpOptions> httpOptions,
        ILogger<HttpAgentToolExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _endpointPolicy = endpointPolicy;
        _credentialService = credentialService;
        _httpOptions = httpOptions.Value;
        _logger = logger;
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(
        AgentTool tool,
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tool.Kind != AgentToolKind.Http)
        {
            return AgentToolExecutionResult.Failure(
                "A Tool informada não é uma Tool HTTP.");
        }

        if (!Uri.TryCreate(
                tool.Endpoint,
                UriKind.Absolute,
                out Uri? endpoint))
        {
            return AgentToolExecutionResult.Failure(
                "O endpoint configurado para a Tool Ã© invÃ¡lido.");
        }

        if (!await _endpointPolicy.IsAllowedAsync(
                endpoint,
                cancellationToken))
        {
            return AgentToolExecutionResult.Failure(
                "O endpoint configurado para a Tool nÃ£o Ã© permitido.");
        }

        ResolvedToolCredential? credential = null;
        if (tool.ToolCredentialId.HasValue)
        {
            credential = await _credentialService.ResolveForExecutionAsync(
                tool.ToolCredentialId.Value,
                tool.TenantId,
                cancellationToken);

            if (credential is null)
            {
                return AgentToolExecutionResult.Failure(
                    "A autenticaÃ§Ã£o configurada para a Tool nÃ£o estÃ¡ disponÃ­vel.");
            }
        }

        try
        {
            using HttpRequestMessage httpRequest =
                CreateHttpRequest(tool, endpoint, request);

            ApplyAuthentication(httpRequest, credential);

            HttpClient client =
                _httpClientFactory.CreateClient("AgentTools");

            using HttpResponseMessage response =
                await client.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            string? content =
                await ReadResponseContentAsync(
                    response.Content,
                    _httpOptions.MaxResponseBytes,
                    cancellationToken);

            if (content is null)
            {
                return AgentToolExecutionResult.Failure(
                    "A resposta da Tool excedeu o tamanho mÃ¡ximo permitido.",
                    (int)response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return AgentToolExecutionResult.Failure(
                    $"A Tool retornou HTTP {(int)response.StatusCode}.",
                    (int)response.StatusCode,
                    content);
            }

            return AgentToolExecutionResult.Success(
                (int)response.StatusCode,
                content);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return AgentToolExecutionResult.Failure(
                "A execuÃ§Ã£o da Tool excedeu o tempo limite.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Erro ao executar Tool {ToolId} para o agente {AgentId}.",
                request.ToolId,
                request.AgentId);

            return AgentToolExecutionResult.Failure(
                "NÃ£o foi possÃ­vel executar a Tool.");
        }
    }

    private static async Task<string?> ReadResponseContentAsync(
        HttpContent content,
        int maxResponseBytes,
        CancellationToken cancellationToken)
    {
        if (maxResponseBytes <= 0)
        {
            throw new InvalidOperationException(
                "MaxResponseBytes deve ser maior que zero.");
        }

        if (content.Headers.ContentLength is long contentLength &&
            contentLength > maxResponseBytes)
        {
            return null;
        }

        await using Stream stream =
            await content.ReadAsStreamAsync(cancellationToken);

        using var buffer = new MemoryStream();

        byte[] chunk = new byte[8192];
        int totalBytes = 0;

        while (true)
        {
            int bytesRead =
                await stream.ReadAsync(
                    chunk.AsMemory(0, chunk.Length),
                    cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;

            if (totalBytes > maxResponseBytes)
            {
                return null;
            }

            await buffer.WriteAsync(
                chunk.AsMemory(0, bytesRead),
                cancellationToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static HttpRequestMessage CreateHttpRequest(
        AgentTool tool,
        Uri endpoint,
        AgentToolExecutionRequest request)
    {
        HttpMethod method = new(tool.HttpMethod);

        var httpRequest =
            new HttpRequestMessage(method, endpoint);

        if (request.Input.HasValue &&
            method != HttpMethod.Get)
        {
            string json = request.Input.Value.GetRawText();

            httpRequest.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");
        }

        return httpRequest;
    }

    private static void ApplyAuthentication(
        HttpRequestMessage request,
        ResolvedToolCredential? credential)
    {
        if (credential is null)
        {
            return;
        }

        switch (credential.AuthenticationType)
        {
            case ToolAuthenticationType.ApiKeyHeader:
                if (!request.Headers.TryAddWithoutValidation(
                    credential.HeaderName,
                    credential.Secret))
                {
                    throw new InvalidOperationException("Header de autenticaÃ§Ã£o de Tool invÃ¡lido.");
                }
                break;
            case ToolAuthenticationType.BearerToken:
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", credential.Secret);
                break;
            default:
                throw new InvalidOperationException("Tipo de autenticaÃ§Ã£o de Tool invÃ¡lido.");
        }
    }
}
