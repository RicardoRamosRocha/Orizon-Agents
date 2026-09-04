using System.Net;
using System.Text;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Infrastructure.Integrations.Gmail;

namespace OrizonAgents.Integration.Tests.Integrations;

public sealed class GmailClientTests
{
    [Fact]
    public async Task SearchMessagesAsync_SendsExpectedRequestAndParsesResponse()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    """
                    {
                      "messages": [
                        {
                          "id": "message-1",
                          "threadId": "thread-1"
                        },
                        {
                          "id": "message-2",
                          "threadId": "thread-2"
                        }
                      ],
                      "nextPageToken": "next-page",
                      "resultSizeEstimate": 42
                    }
                    """)
            });

        var client = CreateClient(handler);

        Guid connectionId = Guid.NewGuid();

        var result = await client.SearchMessagesAsync(
            connectionId,
            "from:cliente@example.com contrato",
            25);

        Assert.Equal(2, result.Messages.Count);

        Assert.Equal("message-1", result.Messages[0].Id);
        Assert.Equal("thread-1", result.Messages[0].ThreadId);

        Assert.Equal("message-2", result.Messages[1].Id);
        Assert.Equal("thread-2", result.Messages[1].ThreadId);

        Assert.Equal("next-page", result.NextPageToken);
        Assert.Equal(42, result.ResultSizeEstimate);

        Assert.Equal(HttpMethod.Get, handler.Method);

        Assert.NotNull(handler.RequestUri);

        Assert.Equal(
            "https",
            handler.RequestUri!.Scheme);

        Assert.Equal(
            "gmail.googleapis.com",
            handler.RequestUri.Host);

        Assert.Equal(
            "/gmail/v1/users/me/messages",
            handler.RequestUri.AbsolutePath);

        Assert.Contains(
            "q=from%3Acliente@example.com%20contrato",
             handler.RequestUri.Query);

        Assert.Contains(
            "maxResults=25",
            handler.RequestUri.Query);

        Assert.Equal(
            "Bearer",
            handler.AuthorizationScheme);

        Assert.Equal(
            "test-access-token",
            handler.AuthorizationParameter);
    }

    [Fact]
    public async Task SearchMessagesAsync_WhenNoMessages_ReturnsEmptyCollection()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    """
                    {
                      "resultSizeEstimate": 0
                    }
                    """)
            });

        var client = CreateClient(handler);

        var result = await client.SearchMessagesAsync(
            Guid.NewGuid(),
            "is:unread");

        Assert.Empty(result.Messages);
        Assert.Null(result.NextPageToken);
        Assert.Equal(0, result.ResultSizeEstimate);
    }

    [Fact]
    public async Task SearchMessagesAsync_RejectsEmptyConnectionId()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SearchMessagesAsync(
                Guid.Empty,
                "is:unread"));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SearchMessagesAsync_RejectsBlankQuery()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SearchMessagesAsync(
                Guid.NewGuid(),
                "   "));

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchMessagesAsync_RejectsInvalidMaxResults(
        int maxResults)
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.SearchMessagesAsync(
                Guid.NewGuid(),
                "is:unread",
                maxResults));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SearchMessagesAsync_WhenTokenCannotBeObtained_DoesNotCallGmail()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK));

        var client = CreateClient(
            handler,
            new StubGoogleOAuthTokenService(
                OperationResult<GoogleAccessToken>.Failure(
                    "A conexão Google precisa ser autenticada.")));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.SearchMessagesAsync(
                    Guid.NewGuid(),
                    "is:unread"));

        Assert.Equal(
            "A conexão Google precisa ser autenticada.",
            exception.Message);

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SearchMessagesAsync_WhenGmailFails_DoesNotExposeResponseBody()
    {
        const string sensitiveProviderBody =
            "SENSITIVE-GMAIL-PROVIDER-RESPONSE";

        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    sensitiveProviderBody,
                    Encoding.UTF8,
                    "application/json")
            });

        var client = CreateClient(handler);

        var exception =
            await Assert.ThrowsAsync<GmailApiException>(
                () => client.SearchMessagesAsync(
                    Guid.NewGuid(),
                    "is:unread"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            exception.StatusCode);

        Assert.DoesNotContain(
            sensitiveProviderBody,
            exception.Message);

        Assert.DoesNotContain(
            "test-access-token",
            exception.Message);
    }

    private static GmailClient CreateClient(
        RecordingHttpMessageHandler handler,
        IGoogleOAuthTokenService? tokenService = null)
    {
        return new GmailClient(
            new FakeHttpClientFactory(handler),
            tokenService ??
            new StubGoogleOAuthTokenService(
                OperationResult<GoogleAccessToken>.Success(
                    new GoogleAccessToken("test-access-token"))));
    }

    private static StringContent Json(string value) =>
        new(
            value,
            Encoding.UTF8,
            "application/json");

    private sealed class StubGoogleOAuthTokenService(
        OperationResult<GoogleAccessToken> result)
        : IGoogleOAuthTokenService
    {
        public Task<OperationResult<GoogleAccessToken>>
            GetAccessTokenAsync(
                Guid connectionId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class FakeHttpClientFactory(
        RecordingHttpMessageHandler handler)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(
                GmailClient.HttpClientName,
                name);

            return new HttpClient(
                handler,
                disposeHandler: false);
        }
    }

    private sealed class RecordingHttpMessageHandler(
        HttpResponseMessage response)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            Method = request.Method;
            RequestUri = request.RequestUri;

            AuthorizationScheme =
                request.Headers.Authorization?.Scheme;

            AuthorizationParameter =
                request.Headers.Authorization?.Parameter;

            return Task.FromResult(response);
        }
    }
}