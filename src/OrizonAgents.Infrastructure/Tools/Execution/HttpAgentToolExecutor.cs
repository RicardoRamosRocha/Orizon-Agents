using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrizonAgents.Application.Tools;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Application.Tools.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class HttpAgentToolExecutor : IAgentToolExecutor
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAgentToolEndpointPolicy _endpointPolicy;
    private readonly IToolCredentialService _credentialService;
    private readonly AgentToolHttpOptions _httpOptions;
    private readonly ILogger<HttpAgentToolExecutor> _logger;

    public HttpAgentToolExecutor(
        OrizonAgentsDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IAgentToolEndpointPolicy endpointPolicy,
        IToolCredentialService credentialService,
        IOptions<AgentToolHttpOptions> httpOptions,
        ILogger<HttpAgentToolExecutor> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _endpointPolicy = endpointPolicy;
        _credentialService = credentialService;
        _httpOptions = httpOptions.Value;
        _logger = logger;
    }

    public async Task<AgentToolExecutionResult> ExecuteAsync(
        AgentToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AgentId == Guid.Empty)
        {
            return AgentToolExecutionResult.Failure(
                "AgentId é obrigatório.");
        }

        if (request.ToolId == Guid.Empty)
        {
            return AgentToolExecutionResult.Failure(
                "ToolId é obrigatório.");
        }

        AgentTool? tool = await (
            from candidateTool in _dbContext.AgentTools.AsNoTracking()
            join binding in _dbContext.AgentToolBindings.AsNoTracking()
                on candidateTool.Id equals binding.ToolId
            join agent in _dbContext.AiAgents.AsNoTracking()
                on binding.AgentId equals agent.Id
            where candidateTool.Id == request.ToolId
                  && agent.Id == request.AgentId
                  && candidateTool.TenantId == agent.TenantId
                  && binding.TenantId == agent.TenantId
                  && candidateTool.IsActive
                  && binding.IsActive
            select candidateTool)
            .SingleOrDefaultAsync(cancellationToken);

        if (tool is null)
        {
            return AgentToolExecutionResult.Failure(
                "Tool não encontrada, inativa ou não vinculada ao agente.");
        }

        if (!Uri.TryCreate(
                tool.Endpoint,
                UriKind.Absolute,
                out Uri? endpoint))
        {
            return AgentToolExecutionResult.Failure(
                "O endpoint configurado para a Tool é inválido.");
        }

        if (!await _endpointPolicy.IsAllowedAsync(
                endpoint,
                cancellationToken))
        {
            return AgentToolExecutionResult.Failure(
                "O endpoint configurado para a Tool não é permitido.");
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
                    "A autenticação configurada para a Tool não está disponível.");
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
                    "A resposta da Tool excedeu o tamanho máximo permitido.",
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
                "A execução da Tool excedeu o tempo limite.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Erro ao executar Tool {ToolId} para o agente {AgentId}.",
                request.ToolId,
                request.AgentId);

            return AgentToolExecutionResult.Failure(
                "Não foi possível executar a Tool.");
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
                    throw new InvalidOperationException("Header de autenticação de Tool inválido.");
                }
                break;
            case ToolAuthenticationType.BearerToken:
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", credential.Secret);
                break;
            default:
                throw new InvalidOperationException("Tipo de autenticação de Tool inválido.");
        }
    }
}
