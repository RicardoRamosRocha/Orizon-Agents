namespace OrizonAgents.Infrastructure.WhatsApp;

public interface IWhatsAppCloudApiClient
{
    Task<WhatsAppCloudNumber> GetPhoneNumberAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default);

    Task<WhatsAppCloudSendResult> SendTextAsync(string accessToken, string phoneNumberId, string recipient, string text, CancellationToken cancellationToken = default);

    Task<WhatsAppCloudSendResult> SendTemplateAsync(string accessToken, string phoneNumberId, string recipient, string templateName, string language, CancellationToken cancellationToken = default);

    Task<WhatsAppCloudSendResult> SendMediaAsync(string accessToken, string phoneNumberId, string recipient, string mediaId, string type, string caption, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WhatsAppCloudTemplate>> GetTemplatesAsync(string accessToken, string businessAccountId, CancellationToken cancellationToken = default);
}
