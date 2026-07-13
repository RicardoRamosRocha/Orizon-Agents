using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Common.Email;

namespace OrizonAgents.Infrastructure.Email;

public sealed class DevelopmentEmailSender : IEmailSender
{
    private readonly ILogger<DevelopmentEmailSender> _logger;

    public DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAccountLinkAsync(
        string email,
        string subject,
        string safeLink,
        CancellationToken cancellationToken = default)
    {
        string redactedLink = RedactToken(safeLink);
        _logger.LogInformation(
            "E-mail de desenvolvimento para {Email}. Assunto: {Subject}. Link: {Link}",
            email,
            subject,
            redactedLink);

        return Task.CompletedTask;
    }

    private static string RedactToken(string link)
    {
        int tokenIndex = link.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
        return tokenIndex < 0 ? link : string.Concat(link.AsSpan(0, tokenIndex), "token=[redacted]");
    }
}
