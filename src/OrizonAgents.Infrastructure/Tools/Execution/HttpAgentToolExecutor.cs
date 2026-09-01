using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Tools.Execution;
using OrizonAgents.Application.Tools.Execution.Models;
using OrizonAgents.Domain.Tools;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Tools.Execution;

public sealed class HttpAgentToolExecutor : IAgentToolExecutor
{
    private readonly OrizonAgentsDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAgentToolEndpointPolicy _endpointPolicy;
    private readonly ILogger<HttpAgentToolExecutor> _logger;

    public HttpAgentToolExecutor(
        OrizonAgentsDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IAgentToolEndpointPolicy endpointPolicy,
        ILogger<HttpAgentToolExecutor> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _endpointPolicy = endpointPolicy;
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

        try
        {
            using HttpRequestMessage httpRequest =
                CreateHttpRequest(tool, endpoint, request);

            HttpClient client =
                _httpClientFactory.CreateClient("AgentTools");

            using HttpResponseMessage response =
                await client.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            string content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

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
}
