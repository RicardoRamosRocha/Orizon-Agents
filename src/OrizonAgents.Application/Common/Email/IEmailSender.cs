namespace OrizonAgents.Application.Common.Email;

public interface IEmailSender
{
    Task SendAccountLinkAsync(
        string email,
        string subject,
        string safeLink,
        CancellationToken cancellationToken = default);
}
