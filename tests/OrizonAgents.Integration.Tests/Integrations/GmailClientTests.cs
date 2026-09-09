using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OrizonAgents.Application.Common.Results;
using OrizonAgents.Domain.Integrations;
using OrizonAgents.Application.Integrations.Gmail;
using OrizonAgents.Application.Integrations.Google;
using OrizonAgents.Infrastructure.Integrations.Gmail;
using OrizonAgents.Infrastructure.Persistence;
using OrizonAgents.Infrastructure.Tenancy;

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
    public async Task SearchMessagesAsync_WithoutQuery_OmitsQAndPreservesMaxResults()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"resultSizeEstimate":0}""")
            });

        var client = CreateClient(handler);

        await client.SearchMessagesAsync(
            Guid.NewGuid(),
            string.Empty,
            3);

        Assert.Equal(1, handler.RequestCount);
        Assert.NotNull(handler.RequestUri);
        Assert.Equal("?maxResults=3", handler.RequestUri!.Query);
        Assert.DoesNotContain("q=", handler.RequestUri.Query);
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

        Assert.Equal("Somente HTML", result.BodyText);
    }

    [Fact]
    public async Task GetMessageAsync_LargeBody_IsReducedAndPreservesMetadata()
    {
        string bodyText =
            "INÍCIO IMPORTANTE\n" +
            new string('x', 10_000) +
            "\nFIM IMPORTANTE";
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    $$"""
                    {
                      "id": "message-large",
                      "threadId": "thread-large",
                      "snippet": "Resumo preservado",
                      "payload": {
                        "mimeType": "text/plain",
                        "headers": [
                          { "name": "Subject", "value": "Assunto preservado" },
                          { "name": "From", "value": "origem@example.com" },
                          { "name": "To", "value": "destino@example.com" },
                          { "name": "Date", "value": "Fri, 04 Sep 2026 12:34:56 -0300" }
                        ],
                        "body": { "data": "{{Base64Url(bodyText)}}" }
                      }
                    }
                    """)
            });

        var result = await CreateClient(handler)
            .GetMessageAsync(Guid.NewGuid(), "message-large");

        Assert.Equal("message-large", result.Id);
        Assert.Equal("thread-large", result.ThreadId);
        Assert.Equal("Assunto preservado", result.Subject);
        Assert.Equal("origem@example.com", result.From);
        Assert.Equal("destino@example.com", result.To);
        Assert.NotNull(result.Date);
        Assert.Equal("Resumo preservado", result.Snippet);
        Assert.NotNull(result.BodyText);
        Assert.True(
            result.BodyText.Length <=
            GmailMessageContentReducer.MaximumBodyCharacters);
        Assert.StartsWith("INÍCIO IMPORTANTE", result.BodyText);
        Assert.EndsWith("FIM IMPORTANTE", result.BodyText);
        Assert.Contains("Trecho intermediário reduzido", result.BodyText);
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

    [Fact]
    public async Task CreateDraftAsync_PostsOnlyServerGeneratedBase64UrlMimeAndReturnsMinimalMetadata()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"id":"draft-1","message":{"id":"message-1","threadId":"thread-1"}}""")
            });

        GmailDraft draft = await CreateClient(handler).CreateDraftAsync(
            Guid.NewGuid(), "destino@example.com", "Assunto \u00e1gil", "Linha 1\nLinha 2");

        Assert.Equal("draft-1", draft.DraftId);
        Assert.Equal("message-1", draft.MessageId);
        Assert.Equal("thread-1", draft.ThreadId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/gmail/v1/users/me/drafts", handler.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("send", handler.RequestUri.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("application/json", handler.ContentType);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        JsonProperty message = Assert.Single(request.RootElement.EnumerateObject());
        Assert.Equal("message", message.Name);
        Assert.Equal(JsonValueKind.Object, message.Value.ValueKind);
        JsonProperty rawProperty = Assert.Single(message.Value.EnumerateObject());
        Assert.Equal("raw", rawProperty.Name);
        string raw = rawProperty.Value.GetString()!;
        Assert.DoesNotContain('=', raw);
        string mime = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw));
        Assert.Contains("To: destino@example.com\r\n", mime);
        Assert.Contains("Content-Type: text/plain; charset=utf-8", mime);
        Assert.Contains("Subject: =?utf-8?B?", mime);
        Assert.Contains("TGlu", mime);
        Assert.DoesNotContain("test-access-token", handler.RequestBody!);
    }

    [Theory]
    [InlineData("destino@example.com\r\nBcc: secret@example.com", "Assunto")]
    [InlineData("destino@example.com", "Assunto\r\nBcc: secret@example.com")]
    public async Task CreateDraftAsync_RejectsHeaderInjectionBeforeTokenOrHttp(
        string to,
        string subject)
    {
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var tokenService = SuccessfulTokenService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateClient(handler, tokenService).CreateDraftAsync(
                Guid.NewGuid(), to, subject, "body"));

        Assert.Equal(0, tokenService.RequestCount);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SendDraftAsync_PostsOnlyDraftIdAndReturnsMinimalMetadata()
    {
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"id":"message-1","threadId":"thread-1"}""")
            });

        GmailSentMessage sent = await CreateClient(handler).SendDraftAsync(
            Guid.NewGuid(), "draft-1");

        Assert.Equal("message-1", sent.MessageId);
        Assert.Equal("thread-1", sent.ThreadId);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/gmail/v1/users/me/drafts/send", handler.RequestUri!.AbsolutePath);
        using JsonDocument payload = JsonDocument.Parse(handler.RequestBody!);
        JsonProperty id = Assert.Single(payload.RootElement.EnumerateObject());
        Assert.Equal("id", id.Name);
        Assert.Equal("draft-1", id.Value.GetString());
        Assert.DoesNotContain("raw", handler.RequestBody!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-access-token", handler.RequestBody!);
    }

    [Fact]
    public async Task ReplyAsync_DerivesThreadAndRecipient_AndSendsOnlyServerGeneratedMime()
    {
        Guid connectionId = Guid.NewGuid();
        await using OrizonAgentsDbContext db = CreateConnectedGmailContext(connectionId, "me@example.com");
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"id":"original","threadId":"thread-1","payload":{"headers":[{"name":"From","value":"Sender <sender@example.com>"},{"name":"To","value":"me@example.com"},{"name":"Subject","value":"Status"},{"name":"Message-ID","value":"<original@example.com>"},{"name":"References","value":"<earlier@example.com>"}]}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"id":"reply-1","threadId":"thread-1"}""")
            });

        GmailReplyMessage reply = await CreateClient(handler, db: db).ReplyAsync(
            connectionId, "original", "Linha 1\nLinha 2");

        Assert.Equal("reply-1", reply.MessageId);
        Assert.Equal("thread-1", reply.ThreadId);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal("/gmail/v1/users/me/messages/original", handler.RequestUris[0]!.AbsolutePath);
        Assert.Equal("/gmail/v1/users/me/messages/send", handler.RequestUris[1]!.AbsolutePath);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBodies[1]!);
        Assert.Equal("thread-1", request.RootElement.GetProperty("threadId").GetString());
        string raw = request.RootElement.GetProperty("raw").GetString()!;
        string mime = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw));
        Assert.Contains("To: sender@example.com\r\n", mime);
        Assert.Contains("Subject: =?utf-8?B?", mime);
        Assert.Contains("UmU6IFN0YXR1cw", mime);
        Assert.Contains("In-Reply-To: <original@example.com>\r\n", mime);
        Assert.Contains("References: <earlier@example.com>\r\n", mime);
        Assert.DoesNotContain("me@example.com", mime);
    }

    [Fact]
    public async Task ReplyAsync_RejectsUntrustedHeaderInjectionBeforeSending()
    {
        Guid connectionId = Guid.NewGuid();
        await using OrizonAgentsDbContext db = CreateConnectedGmailContext(connectionId, "me@example.com");
        var handler = new RecordingHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""{"id":"original","threadId":"thread-1","payload":{"headers":[{"name":"From","value":"sender@example.com"},{"name":"Subject","value":"Status\r\nBcc: attacker@example.com"}]}}""")
            });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateClient(handler, db: db).ReplyAsync(connectionId, "original", "Resposta"));

        Assert.Equal(1, handler.RequestCount);
        Assert.DoesNotContain("send", handler.RequestUris[0]!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    private static GmailClient CreateClient(
        RecordingHttpMessageHandler handler,
        IGoogleOAuthTokenService? tokenService = null,
        OrizonAgentsDbContext? db = null)
    {
        return new GmailClient(
            new FakeHttpClientFactory(handler),
            tokenService ??
            new StubGoogleOAuthTokenService(
                OperationResult<GoogleAccessToken>.Success(
                    new GoogleAccessToken("test-access-token"))),
            new GmailMessageContentReducer(),
            db);
    }

    private static OrizonAgentsDbContext CreateConnectedGmailContext(Guid connectionId, string email)
    {
        var tenant = new CurrentTenant();
        Guid tenantId = Guid.NewGuid();
        tenant.SetTenantId(tenantId);
        var options = new DbContextOptionsBuilder<OrizonAgentsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrizonAgentsDbContext(options, tenant);
        var connection = new IntegrationConnection(tenantId, "Gmail", IntegrationProvider.Gmail);
        connection.Connect(email, "protected-credentials");
        typeof(IntegrationConnection).GetProperty(nameof(IntegrationConnection.Id))!
            .SetValue(connection, connectionId);
        db.IntegrationConnections.Add(connection);
        db.SaveChanges();
        return db;
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
        params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly List<(HttpStatusCode StatusCode, string Body)> _responses = responses
            .Select(response => (response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult()))
            .ToList();
        public int RequestCount { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        public string? RequestBody { get; private set; }
        public string? ContentType { get; private set; }
        public List<Uri?> RequestUris { get; } = [];
        public List<string?> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            if (RequestCount == 1)
            {
                Method = request.Method;
                RequestUri = request.RequestUri;
            }

            AuthorizationScheme =
                request.Headers.Authorization?.Scheme;

            AuthorizationParameter =
                request.Headers.Authorization?.Parameter;

            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            RequestUris.Add(request.RequestUri);
            RequestBodies.Add(RequestBody);
            (HttpStatusCode statusCode, string body) = _responses[Math.Min(RequestCount - 1, _responses.Count - 1)];

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
