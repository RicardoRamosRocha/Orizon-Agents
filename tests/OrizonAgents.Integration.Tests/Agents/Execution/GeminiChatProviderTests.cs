using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using OrizonAgents.Application.Agents.Credentials;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Agents.Execution;

namespace OrizonAgents.Integration.Tests.Agents.Execution;

public sealed class GeminiChatProviderTests
{
    [Fact]
    public async Task CompleteAsync_Retries503UntilSuccess()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.ServiceUnavailable, """{"error":{"message":"busy"}}"""),
            Response(HttpStatusCode.ServiceUnavailable, """{"error":{"message":"busy"}}"""),
            Response(HttpStatusCode.OK, SuccessBody("Resposta recuperada")));

        var provider = CreateProvider(handler);

        string result = await provider.CompleteAsync(
            "gemini-test",
            "system",
            "hello",
            Array.Empty<AiChatMessage>(),
            0.5);

        Assert.Equal("Resposta recuperada", result);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_Retries429UntilSuccess()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.TooManyRequests, """{"error":{"message":"rate limit"}}"""),
            Response(HttpStatusCode.OK, SuccessBody("Resposta após limite")));

        var provider = CreateProvider(handler);

        string result = await provider.CompleteAsync(
            "gemini-test",
            "system",
            "hello",
            Array.Empty<AiChatMessage>(),
            0.5);

        Assert.Equal("Resposta após limite", result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_RetriesTimeoutUntilSuccess()
    {
        var handler = new TimeoutThenSuccessHttpMessageHandler();

        var provider = CreateProvider(handler);

        string result = await provider.CompleteAsync(
            "gemini-test",
            "system",
            "hello",
            Array.Empty<AiChatMessage>(),
            0.5);

        Assert.Equal("Resposta após timeout", result);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotRetryCallerCancellation()
    {
        var handler = new CancellationHttpMessageHandler();

        var provider = CreateProvider(handler);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.CompleteAsync(
                "gemini-test",
                "system",
                "hello",
                Array.Empty<AiChatMessage>(),
                0.5,
                cancellationToken: cancellationSource.Token));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotRetryPermanentError()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.BadRequest, """{"error":{"message":"invalid request"}}"""));

        var provider = CreateProvider(handler);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.CompleteAsync(
                    "gemini-test",
                    "system",
                    "hello",
                    Array.Empty<AiChatMessage>(),
                    0.5));

        Assert.Contains("400", exception.Message);
        Assert.Equal(1, handler.CallCount);
    }

    private static GeminiChatProvider CreateProvider(
        HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress =
                new Uri("https://generativelanguage.googleapis.com/")
        };

        IConfiguration configuration =
            new ConfigurationBuilder().Build();

        return new GeminiChatProvider(
            client,
            configuration,
            new StubCredentialService("test-api-key"));
    }

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                content,
                Encoding.UTF8,
                "application/json")
        };
    }

    private static string SuccessBody(string text)
    {
        return $$"""
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "{{text}}"
                  }
                ]
              }
            }
          ]
        }
        """;
    }

    private sealed class SequenceHttpMessageHandler :
        HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHttpMessageHandler(
            params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "Nenhuma resposta HTTP configurada para esta chamada.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class TimeoutThenSuccessHttpMessageHandler :
        HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            if (CallCount == 1)
            {
                throw new TaskCanceledException(
                    "Simulated HTTP timeout.");
            }

            return Task.FromResult(
                Response(
                    HttpStatusCode.OK,
                    SuccessBody("Resposta após timeout")));
        }
    }

    private sealed class CancellationHttpMessageHandler :
        HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            throw new TaskCanceledException(
                "Simulated caller cancellation.");
        }
    }

    private sealed class StubCredentialService :
        IAiProviderCredentialService
    {
        private readonly string _apiKey;

        public StubCredentialService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public Task<string?> ResolveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(_apiKey);
        }

        public Task SaveAsync(
            AiProvider provider,
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> HasCredentialAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task RemoveAsync(
            AiProvider provider,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
