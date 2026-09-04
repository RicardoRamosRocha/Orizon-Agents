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

    [Fact]
    public async Task GetMessageAsync_ParsesSimplePlainTextMessage()
    {
        const string bodyText = "Olá, esta é uma mensagem simples.";
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    $$"""
                    {
                      "id": "message-1",
                      "threadId": "thread-1",
                      "snippet": "Olá, esta é...",
                      "payload": {
                        "mimeType": "text/plain",
                        "headers": [
                          { "name": "Subject", "value": "Assunto do e-mail" },
                          { "name": "From", "value": "Cliente <cliente@example.com>" },
                          { "name": "To", "value": "Equipe <equipe@example.com>" },
                          { "name": "Date", "value": "Fri, 04 Sep 2026 12:34:56 -0300" }
                        ],
                        "body": { "data": "{{Base64Url(bodyText)}}" }
                      }
                    }
                    """)
            });
        var tokenService = SuccessfulTokenService();
        var client = CreateClient(handler, tokenService);
        Guid connectionId = Guid.NewGuid();

        var result = await client.GetMessageAsync(connectionId, "message-1");

        Assert.Equal("message-1", result.Id);
        Assert.Equal("thread-1", result.ThreadId);
        Assert.Equal("Assunto do e-mail", result.Subject);
        Assert.Equal("Cliente <cliente@example.com>", result.From);
        Assert.Equal("Equipe <equipe@example.com>", result.To);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 4, 12, 34, 56, TimeSpan.FromHours(-3)),
            result.Date);
        Assert.Equal("Olá, esta é...", result.Snippet);
        Assert.Equal(bodyText, result.BodyText);
        Assert.Equal(connectionId, tokenService.LastConnectionId);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/message-1?format=full",
            handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-access-token", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task GetMessageAsync_MultipartAlternative_PrefersPlainText()
    {
        var handler = MessageHandler(
            $$"""
            {
              "mimeType": "multipart/alternative",
              "parts": [
                {
                  "mimeType": "text/html",
                  "body": { "data": "{{Base64Url("<p>Conteúdo HTML</p>")}}" }
                },
                {
                  "mimeType": "text/plain",
                  "body": { "data": "{{Base64Url("Conteúdo em texto")}}" }
                }
              ]
            }
            """);

        var result = await CreateClient(handler)
            .GetMessageAsync(Guid.NewGuid(), "message-alternative");

        Assert.Equal("Conteúdo em texto", result.BodyText);
    }

    [Fact]
    public async Task GetMessageAsync_WhenPlainTextDoesNotExist_UsesHtml()
    {
        var handler = MessageHandler(
            $$"""
            {
              "mimeType": "multipart/alternative",
              "parts": [
                {
                  "mimeType": "text/html",
                  "body": { "data": "{{Base64Url("<p>Somente HTML</p>")}}" }
                }
              ]
            }
            """);

        var result = await CreateClient(handler)
            .GetMessageAsync(Guid.NewGuid(), "message-html");

        Assert.Equal("<p>Somente HTML</p>", result.BodyText);
    }

    [Fact]
    public async Task GetMessageAsync_FindsPlainTextInNestedMultipart()
    {
        var handler = MessageHandler(
            $$"""
            {
              "mimeType": "multipart/mixed",
              "parts": [
                {
                  "mimeType": "multipart/related",
                  "parts": [
                    {
                      "mimeType": "multipart/alternative",
                      "parts": [
                        {
                          "mimeType": "text/html",
                          "body": { "data": "{{Base64Url("<p>Aninhado HTML</p>")}}" }
                        },
                        {
                          "mimeType": "text/plain",
                          "body": { "data": "{{Base64Url("Texto aninhado")}}" }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        var result = await CreateClient(handler)
            .GetMessageAsync(Guid.NewGuid(), "message-nested");

        Assert.Equal("Texto aninhado", result.BodyText);
    }

    [Fact]
    public async Task GetMessageAsync_DecodesBase64UrlAlphabetAndMissingPadding()
    {
        const string bodyText = "࠾࠿";
        string encoded = Base64Url(bodyText);
        Assert.Contains("-", encoded);
        Assert.Contains("_", encoded);
        Assert.DoesNotContain("=", encoded);
        var handler = MessageHandler(
            $$"""
            {
              "mimeType": "text/plain",
              "body": { "data": "{{encoded}}" }
            }
            """);

        var result = await CreateClient(handler)
            .GetMessageAsync(Guid.NewGuid(), "message-base64url");

        Assert.Equal(bodyText, result.BodyText);
    }

    [Fact]
    public async Task GetMessageAsync_RejectsEmptyConnectionId()
    {
        var handler = MessageHandler("""{ "mimeType": "text/plain" }""");
        var tokenService = SuccessfulTokenService();
        var client = CreateClient(handler, tokenService);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetMessageAsync(Guid.Empty, "message-1"));

        Assert.Equal(0, tokenService.RequestCount);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetMessageAsync_RejectsBlankMessageId()
    {
        var handler = MessageHandler("""{ "mimeType": "text/plain" }""");
        var tokenService = SuccessfulTokenService();
        var client = CreateClient(handler, tokenService);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetMessageAsync(Guid.NewGuid(), "   "));

        Assert.Equal(0, tokenService.RequestCount);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetMessageAsync_WhenTokenCannotBeObtained_DoesNotCallGmail()
    {
        var handler = MessageHandler("""{ "mimeType": "text/plain" }""");
        var client = CreateClient(
            handler,
            new StubGoogleOAuthTokenService(
                OperationResult<GoogleAccessToken>.Failure(
                    "A conexão Google precisa ser autenticada.")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetMessageAsync(Guid.NewGuid(), "message-1"));

        Assert.Equal(
            "A conexão Google precisa ser autenticada.",
            exception.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetMessageAsync_WhenGmailFails_DoesNotExposeResponseBodyOrToken()
    {
        const string sensitiveProviderBody =
            "SENSITIVE-GMAIL-MESSAGE-RESPONSE";
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(
                    sensitiveProviderBody,
                    Encoding.UTF8,
                    "application/json")
            });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<GmailApiException>(
            () => client.GetMessageAsync(Guid.NewGuid(), "message-1"));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.DoesNotContain(sensitiveProviderBody, exception.Message);
        Assert.DoesNotContain("test-access-token", exception.Message);
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

    private static StubGoogleOAuthTokenService SuccessfulTokenService() =>
        new(
            OperationResult<GoogleAccessToken>.Success(
                new GoogleAccessToken("test-access-token")));

    private static RecordingHttpMessageHandler MessageHandler(string payload) =>
        new(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    $$"""
                    {
                      "id": "message-id",
                      "threadId": "thread-id",
                      "snippet": "snippet",
                      "payload": {{payload}}
                    }
                    """)
            });

    private static string Base64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static StringContent Json(string value) =>
        new(
            value,
            Encoding.UTF8,
            "application/json");

    private sealed class StubGoogleOAuthTokenService(
        OperationResult<GoogleAccessToken> result)
        : IGoogleOAuthTokenService
    {
        public int RequestCount { get; private set; }
        public Guid? LastConnectionId { get; private set; }

        public Task<OperationResult<GoogleAccessToken>>
            GetAccessTokenAsync(
                Guid connectionId,
                CancellationToken cancellationToken = default)
        {
            RequestCount++;
            LastConnectionId = connectionId;
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
